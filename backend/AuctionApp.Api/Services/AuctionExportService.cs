using System.Globalization;
using System.Text;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;

namespace AuctionApp.Api.Services;

/// <summary>
/// The auction as a spreadsheet: every squad with what it paid, and the full bid history.
/// </summary>
public interface IAuctionExportService
{
    /// <summary>One row per signing, plus the unsold and un-called players.</summary>
    Task<string> SquadsCsvAsync(int auctionId, CancellationToken ct = default);

    /// <summary>One row per bid, oldest first — the audit trail of how each price was reached.</summary>
    Task<string> BidsCsvAsync(int auctionId, CancellationToken ct = default);
}

public class AuctionExportService : IAuctionExportService
{
    private readonly IUnitOfWork _uow;

    public AuctionExportService(IUnitOfWork uow) => _uow = uow;

    public async Task<string> SquadsCsvAsync(int auctionId, CancellationToken ct = default)
    {
        var auction = await _uow.Auctions.GetWithDetailsAsync(auctionId, ct)
            ?? throw new NotFoundException($"Auction {auctionId} was not found.");

        var teams = await _uow.Teams.ListForTournamentAsync(auction.TournamentId, ct);
        var players = await _uow.Players.ListForTournamentAsync(auction.TournamentId, ct);
        var teamsById = teams.ToDictionary(t => t.Id);

        var csv = new StringBuilder();
        csv.AppendLine("Team,Team short name,Squad size,Purse remaining,Total spent,Jersey,Lot,Player,Role,Gender,Country,Status,Price,Sold at (UTC)");

        // Squads first, each team's signings grouped and dearest first — the order a results
        // sheet is actually read in.
        foreach (var team in teams.OrderBy(t => t.Name))
        {
            var squad = players
                .Where(p => p.SoldToTeamId == team.Id && p.Status == PlayerStatus.Sold)
                .OrderByDescending(p => p.SoldPrice ?? 0m)
                .ToList();

            var spent = squad.Sum(p => p.SoldPrice ?? 0m);

            if (squad.Count == 0)
            {
                csv.AppendLine(string.Join(',', [
                    Escape(team.Name), Escape(team.ShortName), "0", Number(team.Purse), "0",
                    "", "", "(no signings)", "", "", "", "", "", "",
                ]));
                continue;
            }

            foreach (var player in squad)
            {
                csv.AppendLine(string.Join(',', [
                    Escape(team.Name),
                    Escape(team.ShortName),
                    squad.Count.ToString(CultureInfo.InvariantCulture),
                    Number(team.Purse),
                    Number(spent),
                    player.JerseyNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    player.LotOrder.ToString(CultureInfo.InvariantCulture),
                    Escape(player.Name),
                    Escape(player.Role),
                    player.Gender.ToString(),
                    Escape(player.Country),
                    "Sold",
                    Number(player.SoldPrice ?? 0m),
                    player.SoldAt?.ToString("u", CultureInfo.InvariantCulture) ?? string.Empty,
                ]));
            }
        }

        // Everyone who did not end up on a squad, so the sheet accounts for the whole pool.
        foreach (var player in players
                     .Where(p => p.Status != PlayerStatus.Sold)
                     .OrderBy(p => p.LotOrder))
        {
            csv.AppendLine(string.Join(',', [
                "(unsigned)", "", "", "", "",
                player.JerseyNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                player.LotOrder.ToString(CultureInfo.InvariantCulture),
                Escape(player.Name),
                Escape(player.Role),
                player.Gender.ToString(),
                Escape(player.Country),
                player.Status.ToString(),
                Number(player.BasePrice),
                string.Empty,
            ]));
        }

        _ = teamsById;
        return csv.ToString();
    }

    public async Task<string> BidsCsvAsync(int auctionId, CancellationToken ct = default)
    {
        _ = await _uow.Auctions.GetWithDetailsAsync(auctionId, ct)
            ?? throw new NotFoundException($"Auction {auctionId} was not found.");

        // Large enough to cover a full evening; the cap keeps one request from pulling an
        // unbounded table into memory.
        var bids = await _uow.Bids.ListRecentAsync(auctionId, 10_000, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Placed at (UTC),Player,Jersey,Team,Amount,Entered by auctioneer");

        foreach (var bid in bids.OrderBy(b => b.Id))
        {
            csv.AppendLine(string.Join(',', [
                bid.PlacedAt.ToString("u", CultureInfo.InvariantCulture),
                Escape(bid.Player?.Name),
                bid.Player?.JerseyNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Escape(bid.Team?.Name),
                Number(bid.Amount),
                bid.IsManualOverride ? "yes" : "no",
            ]));
        }

        return csv.ToString();
    }

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// RFC 4180 quoting. Team names carry commas and apostrophes often enough that skipping
    /// this would shift columns in the middle of someone's results sheet.
    /// </summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }
}
