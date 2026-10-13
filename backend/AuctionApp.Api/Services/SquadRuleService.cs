using AuctionApp.Api.Contracts;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;

namespace AuctionApp.Api.Services;

/// <summary>
/// Enforces a tournament's squad rules and reports how far each team is from satisfying them.
///
/// The rules only bite together. A minimum-women requirement is unenforceable on its own —
/// a team can simply spend its purse on men and be unable to comply. The purse reserve is
/// what makes it hold: a bid is refused when winning it would leave the team unable to fill
/// its remaining mandatory slots at the cheapest price still on the table.
/// </summary>
public interface ISquadRuleService
{
    /// <summary>Standings for every team in the tournament, measured against the lot on the block.</summary>
    Task<IReadOnlyList<TeamSummaryDto>> BuildStandingsAsync(
        Tournament tournament,
        Player? currentPlayer,
        CancellationToken ct = default);

    /// <summary>
    /// Throws <see cref="AuctionRuleException"/> when this bid would break a squad rule.
    /// Called before a bid is recorded and again before the hammer falls.
    /// </summary>
    Task EnsureBidAllowedAsync(
        Tournament tournament,
        Team team,
        Player player,
        decimal amount,
        CancellationToken ct = default);
}

public class SquadRuleService : ISquadRuleService
{
    private readonly IUnitOfWork _uow;

    public SquadRuleService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<TeamSummaryDto>> BuildStandingsAsync(
        Tournament tournament,
        Player? currentPlayer,
        CancellationToken ct = default)
    {
        var teams = await _uow.Teams.ListForTournamentAsync(tournament.Id, ct);
        var floors = await LoadPriceFloorsAsync(tournament.Id, ct);

        return teams.Select(team => Summarise(tournament, team, currentPlayer, floors)).ToList();
    }

    public async Task EnsureBidAllowedAsync(
        Tournament tournament,
        Team team,
        Player player,
        decimal amount,
        CancellationToken ct = default)
    {
        var squad = await SquadOfAsync(team, ct);
        var label = team.Name;

        if (tournament.MaxSquadSize > 0 && squad.Count >= tournament.MaxSquadSize)
        {
            throw new AuctionRuleException(
                $"{label} already holds the maximum squad of {tournament.MaxSquadSize} players.");
        }

        if (tournament.MaxSpendPerPlayer is decimal cap && amount > cap)
        {
            throw new AuctionRuleException(
                $"This tournament caps a single signing at {cap:N0}.");
        }

        var womenWon = squad.Count(p => p.Gender == Gender.Female);
        var womenStillNeeded = Math.Max(0, tournament.MinWomenPerSquad - womenWon);

        // Signing a man is refused outright when it would leave too few slots for the
        // women the team still owes. Better to block the bid than to let a squad become
        // impossible to complete.
        if (player.Gender != Gender.Female && tournament.MaxSquadSize > 0)
        {
            var slotsLeftAfterWin = tournament.MaxSquadSize - squad.Count - 1;
            if (slotsLeftAfterWin < womenStillNeeded)
            {
                throw new AuctionRuleException(
                    $"{label} must still sign {Plural(womenStillNeeded, "woman", "women")} and would " +
                    $"have only {Plural(slotsLeftAfterWin, "slot", "slots")} left. Bid on a woman player instead.");
            }
        }

        if (amount > team.Purse)
        {
            throw new AuctionRuleException($"{label} has only {team.Purse:N0} left in the purse.");
        }

        if (!tournament.ReservePurseForUnfilledSlots)
        {
            return;
        }

        var floors = await LoadPriceFloorsAsync(tournament.Id, ct);
        var reserve = ReserveAfterWinning(tournament, squad.Count, womenWon, player, floors);

        if (reserve > 0 && amount > team.Purse - reserve)
        {
            var spendable = Math.Max(0m, team.Purse - reserve);
            throw new AuctionRuleException(
                $"{label} must keep {reserve:N0} back to fill its remaining required slots, " +
                $"so it can bid at most {spendable:N0} on this player.");
        }
    }

    /// <summary>
    /// Cheapest base price still in the pool, overall and among women. These are what the
    /// reserve is priced at — the most optimistic cost of completing a squad.
    /// </summary>
    private async Task<PriceFloors> LoadPriceFloorsAsync(int tournamentId, CancellationToken ct)
    {
        var any = await _uow.Players.CheapestAvailableBaseAsync(tournamentId, null, ct);
        var women = await _uow.Players.CheapestAvailableBaseAsync(tournamentId, Gender.Female, ct);
        return new PriceFloors(any ?? 0m, women ?? any ?? 0m);
    }

    /// <summary>
    /// Everyone who occupies a place in the squad — bought at auction or retained. The rules
    /// are about the shape of the final squad, so a marquee signing fills a slot exactly as a
    /// bought player does.
    /// </summary>
    private static bool IsSquadMember(Player player) =>
        player.Status is PlayerStatus.Sold or PlayerStatus.Retained;

    private async Task<List<Player>> SquadOfAsync(Team team, CancellationToken ct)
    {
        // Teams loaded for a write path may not carry their squad, so top it up on demand.
        if (team.Players is { Count: > 0 })
        {
            return team.Players.Where(IsSquadMember).ToList();
        }

        var loaded = await _uow.Teams.GetWithSquadAsync(team.Id, ct);
        return loaded?.Players.Where(IsSquadMember).ToList() ?? [];
    }

    private static TeamSummaryDto Summarise(
        Tournament tournament,
        Team team,
        Player? currentPlayer,
        PriceFloors floors)
    {
        // The squad is bought players plus retained ones; "won" stays the bought count,
        // because that is what the money and the sold tally refer to.
        var squad = team.Players.Where(IsSquadMember).ToList();
        var bought = squad.Where(p => p.Status == PlayerStatus.Sold).ToList();
        var retained = squad
            .Where(p => p.Status == PlayerStatus.Retained)
            .OrderBy(p => p.TeamRole == TeamRole.Owner ? 0 : 1)
            .ThenBy(p => p.Name)
            .ToList();

        var squadSize = squad.Count;
        var playersWon = bought.Count;
        var womenInSquad = squad.Count(p => p.Gender == Gender.Female);

        var slotsRemaining = Math.Max(0, tournament.MinSquadSize - squadSize);
        var womenRequired = Math.Max(0, tournament.MinWomenPerSquad - womenInSquad);

        // Priced against the lot actually on the block, so the number on screen is the
        // number that will be enforced on the next bid.
        var reserve = tournament.ReservePurseForUnfilledSlots
            ? ReserveAfterWinning(tournament, squadSize, womenInSquad, currentPlayer, floors)
            : 0m;

        var spendable = Math.Max(0m, team.Purse - reserve);
        if (tournament.MaxSpendPerPlayer is decimal cap)
        {
            spendable = Math.Min(spendable, cap);
        }

        string? blocked = null;
        if (tournament.MaxSquadSize > 0 && squadSize >= tournament.MaxSquadSize)
        {
            blocked = "Squad full";
        }
        else if (spendable <= 0m)
        {
            blocked = reserve > 0 ? "Purse reserved for required slots" : "Purse exhausted";
        }
        else if (currentPlayer is not null &&
                 currentPlayer.Gender != Gender.Female &&
                 tournament.MaxSquadSize > 0 &&
                 tournament.MaxSquadSize - squadSize - 1 < womenRequired)
        {
            blocked = $"Needs {Plural(womenRequired, "woman", "women")}";
        }

        return new TeamSummaryDto(
            team.Id,
            team.Name,
            team.ShortName,
            team.OwnerUserId,
            team.Purse,
            team.InitialPurse,
            playersWon,
            womenInSquad,
            bought.Sum(p => p.SoldPrice ?? 0m),
            slotsRemaining,
            womenRequired,
            reserve,
            spendable,
            blocked is null,
            blocked,
            squadSize,
            retained.Select(p => p.ToMemberDto()).ToList());
    }

    /// <summary>
    /// What the team would still have to keep back if it won <paramref name="player"/>.
    /// Women slots are priced at the cheapest woman left, the rest at the cheapest player left.
    /// </summary>
    private static decimal ReserveAfterWinning(
        Tournament tournament,
        int squadSize,
        int womenInSquad,
        Player? player,
        PriceFloors floors)
    {
        // With no lot on the block, price the reserve as it stands right now.
        var winsAWoman = player?.Gender == Gender.Female;
        var signings = player is null ? 0 : 1;

        var squadAfter = squadSize + signings;
        var womenAfter = womenInSquad + (winsAWoman ? 1 : 0);

        var slotsOwed = Math.Max(0, tournament.MinSquadSize - squadAfter);
        var womenOwed = Math.Max(0, tournament.MinWomenPerSquad - womenAfter);

        // Women count towards the squad minimum, so only the excess needs generic pricing.
        var genericSlots = Math.Max(0, slotsOwed - womenOwed);

        return (womenOwed * floors.CheapestWoman) + (genericSlots * floors.CheapestAny);
    }

    private static string Plural(int count, string one, string many) =>
        $"{count} {(count == 1 ? one : many)}";

    private readonly record struct PriceFloors(decimal CheapestAny, decimal CheapestWoman);
}
