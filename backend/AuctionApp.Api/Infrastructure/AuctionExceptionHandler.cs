using AuctionApp.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AuctionApp.Api.Infrastructure;

/// <summary>
/// Turns domain exceptions into ProblemDetails the UI can display verbatim.
/// Anything else falls through to the framework handler so unexpected failures are
/// not leaked to clients as friendly text.
/// </summary>
public class AuctionExceptionHandler : IExceptionHandler
{
    private readonly ILogger<AuctionExceptionHandler> _logger;

    public AuctionExceptionHandler(ILogger<AuctionExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            AuctionRuleException => (StatusCodes.Status409Conflict, "Auction rule violated"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Not yours to change"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Not authorized"),
            _ => (0, string.Empty),
        };

        if (status == 0)
        {
            _logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);
            return false;
        }

        _logger.LogInformation("{Title} on {Path}: {Message}", title, httpContext.Request.Path, exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
