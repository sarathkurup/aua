using System.Text;
using System.Text.Json.Serialization;
using AuctionApp.Api.Data;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Hubs;
using AuctionApp.Api.Infrastructure;
using AuctionApp.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

const string CorsPolicy = "AuctionAppClient";

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- configuration
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// Fail at startup rather than issuing tokens signed with a weak or empty key.
if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 bytes. Set it via user-secrets, an environment " +
        "variable (Jwt__Key), or appsettings for the current environment.");
}

// ---------------------------------------------------------------- data access
builder.Services.AddDbContext<AuctionDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AuctionDb"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));

builder.Services.AddScoped<ISportRepository, SportRepository>();
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
builder.Services.AddScoped<IAuctionRepository, AuctionRepository>();
builder.Services.AddScoped<IBidRepository, BidRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ---------------------------------------------------------------- application services
// The lock provider is a singleton: it is the process-wide gate that serializes
// competing bids on the same auction.
builder.Services.AddSingleton<IAuctionLockProvider, AuctionLockProvider>();

// Viewer counts live for the lifetime of the process, alongside the connections they track.
builder.Services.AddSingleton<IRoomPresence, RoomPresence>();
// Ownership checks read the caller off the current request rather than having every
// service signature carry a user id through it.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ITournamentAccess, TournamentAccess>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuctionNotifier, AuctionNotifier>();
builder.Services.AddScoped<IAccountRequestService, AccountRequestService>();
builder.Services.AddScoped<ISquadRuleService, SquadRuleService>();
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IBidService, BidService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();

builder.Services.AddScoped<IAuctionExportService, AuctionExportService>();

// Enforces the 90-day retention promise stated in the README.
builder.Services.AddHostedService<AuctionRetentionService>();

// Closes lots whose countdown has run out, so a shut browser cannot stall a room.
builder.Services.AddHostedService<AuctionTimerService>();

// Singleton: it only touches the filesystem and holds no per-request state.
builder.Services.AddSingleton<IPhotoService, PhotoService>();

// ---------------------------------------------------------------- authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // A browser WebSocket cannot send an Authorization header, so SignalR passes the
        // token as a query parameter. Accept it for hub paths only.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(nameof(UserRole.Admin))));

// ---------------------------------------------------------------- web
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Echo any origin so the app can be opened from a phone on the same LAN
        // during responsive testing. Production uses the configured allow-list.
        policy.SetIsOriginAllowed(_ => true);
    }
    else
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []);
    }

    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        // Statuses and roles cross the wire as readable names, not ordinals.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AuctionExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Skipped in development so a phone on the LAN can reach the API over plain HTTP.
    app.UseHttpsRedirection();
}

// Serves the published React build when it has been copied into wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

// Uploaded player photos, stored outside wwwroot and mapped explicitly. Inside wwwroot the
// default provider would answer first and drop the immutable cache header set below.
var mediaRoot = Path.Combine(app.Environment.ContentRootPath, "media");
Directory.CreateDirectory(mediaRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRoot),
    RequestPath = "/media",
    OnPrepareResponse = context =>
    {
        // Photo filenames are content hashes, so a given URL can never change meaning.
        // Cache them for a year: after the first sight of a player no screen fetches that
        // image again, which is what keeps a called lot instant.
        context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    },
});

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AuctionHub>("/hubs/auction");

var spaIndex = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
if (File.Exists(spaIndex))
{
    app.MapFallbackToFile("index.html");
}

await MigrateAndSeedAsync(app);

app.Run();

/// <summary>
/// Brings the database up to the current migration and fills it with starter data.
/// Runs on every boot; both steps are no-ops once satisfied.
/// </summary>
static async Task MigrateAndSeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var db = services.GetRequiredService<AuctionDbContext>();
        await db.Database.MigrateAsync();

        var seedOptions = services.GetRequiredService<IOptions<SeedOptions>>().Value;
        await DbSeeder.SeedAsync(db, seedOptions, logger);

        logger.LogInformation("Database ready.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration or seeding failed. The API cannot serve requests.");
        throw;
    }
}
