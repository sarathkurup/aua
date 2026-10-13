using AuctionApp.Api.Data;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Infrastructure;
using AuctionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Controllers;

/// <summary>
/// Test fixtures, for manual testing on a developer machine.
///
/// Refuses to do anything outside Development. It writes a whole tournament's worth of
/// players and photos, which is not something a production instance should be one request
/// away from.
/// </summary>
[ApiController]
[Route("api/dev")]
[Authorize(Policy = Policies.AdminOnly)]
public class DevDataController : ControllerBase
{
    private readonly AuctionDbContext _db;
    private readonly IPhotoService _photos;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DevDataController> _logger;

    public DevDataController(
        AuctionDbContext db,
        IPhotoService photos,
        IWebHostEnvironment environment,
        ILogger<DevDataController> logger)
    {
        _db = db;
        _photos = photos;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Creates a badminton tournament whose players carry photos in every orientation —
    /// portrait, landscape, square and two extremes — so the UI can be checked against all
    /// of them. Safe to call twice: it returns the existing one rather than duplicating it.
    /// </summary>
    [HttpPost("mock-data")]
    public async Task<ActionResult<object>> SeedMockData(CancellationToken ct)
    {
        RequireDevelopment();

        var tournamentId = await MockDataSeeder.SeedAsync(_db, _photos, _logger, ct);
        var players = await _db.Players.CountAsync(p => p.TournamentId == tournamentId, ct);

        return Ok(new
        {
            tournamentId,
            name = MockDataSeeder.TournamentName,
            players,
            message = "Mock tournament ready. Delete the tournament to remove it.",
        });
    }

    private void RequireDevelopment()
    {
        if (!_environment.IsDevelopment())
        {
            throw new AuctionRuleException("Test data can only be created on a development machine.");
        }
    }
}
