using AuctionApp.Api.Contracts;
using AuctionApp.Api.Data;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;
using AuctionApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Tests;

/// <summary>
/// The squad rules, exercised against a real DbContext rather than mocks — the reserve
/// calculation depends on live queries for the cheapest player left in the pool, and stubbing
/// those would only prove the stubs agree with themselves.
/// </summary>
public class SquadRuleServiceTests : IDisposable
{
    private readonly AuctionDbContext _db;
    private readonly ISquadRuleService _rules;

    public SquadRuleServiceTests()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseInMemoryDatabase($"squad-rules-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new AuctionDbContext(options);

        var uow = new UnitOfWork(
            _db,
            new SportRepository(_db),
            new TournamentRepository(_db),
            new TeamRepository(_db),
            new PlayerRepository(_db),
            new AuctionRepository(_db),
            new BidRepository(_db),
            new UserRepository(_db));

        _rules = new SquadRuleService(uow);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------ fixture

    /// <summary>
    /// A workplace badminton tournament: squads of at least four including two women, and no
    /// upper limit on size — which is how the real leagues run.
    /// </summary>
    private Tournament NewTournament(
        int minSquad = 4,
        int maxSquad = 0,
        int minWomen = 2,
        decimal? maxSpend = null,
        bool reserve = true,
        decimal purse = 1_000_000m)
    {
        var sport = new Sport { Id = 1, Name = "Badminton" };
        var tournament = new Tournament
        {
            Id = 1,
            Name = "Inter-Office",
            SportId = sport.Id,
            Sport = sport,
            Type = TournamentType.Corporate,
            TeamPurse = purse,
            MinSquadSize = minSquad,
            MaxSquadSize = maxSquad,
            MinWomenPerSquad = minWomen,
            MaxSpendPerPlayer = maxSpend,
            ReservePurseForUnfilledSlots = reserve,
        };

        _db.Sports.Add(sport);
        _db.Tournaments.Add(tournament);
        return tournament;
    }

    private Team NewTeam(Tournament tournament, string name = "Platform", decimal? purse = null)
    {
        var team = new Team
        {
            Id = _db.Teams.Local.Count + 1,
            TournamentId = tournament.Id,
            Name = name,
            Purse = purse ?? tournament.TeamPurse,
            InitialPurse = tournament.TeamPurse,
        };

        _db.Teams.Add(team);
        return team;
    }

    private Player NewPlayer(
        Tournament tournament,
        string name,
        decimal basePrice,
        Gender gender = Gender.Male,
        PlayerStatus status = PlayerStatus.Available,
        Team? team = null,
        TeamRole teamRole = TeamRole.Auctioned,
        decimal? soldPrice = null)
    {
        var player = new Player
        {
            Id = _db.Players.Local.Count + 100,
            Name = name,
            TournamentId = tournament.Id,
            Tournament = tournament,
            BasePrice = basePrice,
            Gender = gender,
            Status = status,
            TeamRole = teamRole,
            SoldToTeamId = team?.Id,
            SoldPrice = soldPrice,
        };

        // Only the foreign key is set. Adding to team.Players as well would leave the player
        // in the collection twice once EF fixes up navigations on save.
        _db.Players.Add(player);
        return player;
    }

    private async Task<TeamSummaryDto> StandingFor(Tournament tournament, Team team, Player? onBlock = null)
    {
        await _db.SaveChangesAsync();
        var standings = await _rules.BuildStandingsAsync(tournament, onBlock);
        return standings.Single(s => s.TeamId == team.Id);
    }

    // ------------------------------------------------------------------ retained members

    [Fact]
    public async Task Retained_members_count_towards_the_squad()
    {
        var tournament = NewTournament(minSquad: 4);
        var team = NewTeam(tournament);

        NewPlayer(tournament, "Owner", 0, Gender.Male, PlayerStatus.Retained, team, TeamRole.Owner);
        NewPlayer(tournament, "Marquee A", 0, Gender.Male, PlayerStatus.Retained, team, TeamRole.Marquee);
        NewPlayer(tournament, "Marquee B", 0, Gender.Male, PlayerStatus.Retained, team, TeamRole.Marquee);
        NewPlayer(tournament, "Pool", 50_000m);

        var standing = await StandingFor(tournament, team);

        Assert.Equal(3, standing.SquadSize);
        Assert.Equal(0, standing.PlayersWon);
        Assert.Equal(1, standing.SlotsRemaining);
        Assert.Equal(3, standing.Retained.Count);
        Assert.Equal(TeamRole.Owner, standing.Retained.First().TeamRole);
    }

    [Fact]
    public async Task A_retained_woman_counts_towards_the_participation_rule()
    {
        var tournament = NewTournament(minWomen: 2);
        var team = NewTeam(tournament);

        NewPlayer(tournament, "Owner", 0, Gender.Female, PlayerStatus.Retained, team, TeamRole.Owner);
        NewPlayer(tournament, "Pool", 50_000m, Gender.Female);

        var standing = await StandingFor(tournament, team);

        Assert.Equal(1, standing.WomenWon);
        Assert.Equal(1, standing.WomenRequired);
    }

    /// <summary>Marquee signings are usually men, so they do not shorten the women requirement.</summary>
    [Fact]
    public async Task Marquee_men_do_not_satisfy_the_women_requirement()
    {
        var tournament = NewTournament(minWomen: 2);
        var team = NewTeam(tournament);

        NewPlayer(tournament, "Owner", 0, Gender.Male, PlayerStatus.Retained, team, TeamRole.Owner);
        NewPlayer(tournament, "Marquee A", 0, Gender.Male, PlayerStatus.Retained, team, TeamRole.Marquee);
        NewPlayer(tournament, "Marquee B", 0, Gender.Male, PlayerStatus.Retained, team, TeamRole.Marquee);
        NewPlayer(tournament, "Pool woman", 50_000m, Gender.Female);

        var standing = await StandingFor(tournament, team);

        Assert.Equal(0, standing.WomenWon);
        Assert.Equal(2, standing.WomenRequired);
    }

    /// <summary>The squads run to 15 or 20, so nothing may declare them full.</summary>
    [Fact]
    public async Task With_no_maximum_squad_size_a_large_squad_can_still_bid()
    {
        var tournament = NewTournament(minSquad: 4, maxSquad: 0, minWomen: 0, reserve: false);
        var team = NewTeam(tournament);

        for (var i = 0; i < 20; i++)
        {
            NewPlayer(tournament, $"Bought {i}", 0, Gender.Male, PlayerStatus.Sold, team, soldPrice: 1_000m);
        }

        var lot = NewPlayer(tournament, "Next", 10_000m);
        var standing = await StandingFor(tournament, team, lot);

        Assert.Equal(20, standing.SquadSize);
        Assert.True(standing.CanBid);
        Assert.Null(standing.BlockedReason);

        await _rules.EnsureBidAllowedAsync(tournament, team, lot, 10_000m);
    }

    // ------------------------------------------------------------------ money rules

    [Fact]
    public async Task A_bid_above_the_per_player_cap_is_refused()
    {
        var tournament = NewTournament(minSquad: 0, minWomen: 0, maxSpend: 100_000m, reserve: false);
        var team = NewTeam(tournament);
        var lot = NewPlayer(tournament, "Star", 50_000m);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AuctionRuleException>(
            () => _rules.EnsureBidAllowedAsync(tournament, team, lot, 100_001m));

        Assert.Contains("caps a single signing", error.Message);
    }

    [Fact]
    public async Task A_bid_beyond_the_purse_is_refused()
    {
        var tournament = NewTournament(minSquad: 0, minWomen: 0, reserve: false, purse: 10_000m);
        var team = NewTeam(tournament);
        var lot = NewPlayer(tournament, "Star", 1_000m);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AuctionRuleException>(
            () => _rules.EnsureBidAllowedAsync(tournament, team, lot, 10_001m));

        Assert.Contains("left in the purse", error.Message);
    }

    /// <summary>
    /// The rule that makes the women requirement enforceable: a team may not spend its way
    /// into a squad it can no longer complete.
    /// </summary>
    [Fact]
    public async Task The_reserve_stops_a_team_spending_its_way_out_of_compliance()
    {
        var tournament = NewTournament(minSquad: 3, minWomen: 2, reserve: true, purse: 100_000m);
        var team = NewTeam(tournament);

        var man = NewPlayer(tournament, "Man on block", 10_000m, Gender.Male);
        NewPlayer(tournament, "Woman A", 20_000m, Gender.Female);
        NewPlayer(tournament, "Woman B", 20_000m, Gender.Female);
        await _db.SaveChangesAsync();

        // Winning him leaves 2 women owed at 20,000 each, so at most 60,000 may go on this lot.
        var standing = await StandingFor(tournament, team, man);
        Assert.Equal(40_000m, standing.ReservedPurse);
        Assert.Equal(60_000m, standing.SpendablePurse);

        await _rules.EnsureBidAllowedAsync(tournament, team, man, 60_000m);

        var error = await Assert.ThrowsAsync<AuctionRuleException>(
            () => _rules.EnsureBidAllowedAsync(tournament, team, man, 60_001m));

        Assert.Contains("must keep", error.Message);
    }

    [Fact]
    public async Task Retained_members_shrink_the_reserve_because_fewer_slots_remain()
    {
        var tournament = NewTournament(minSquad: 3, minWomen: 1, reserve: true, purse: 100_000m);
        var team = NewTeam(tournament);

        NewPlayer(tournament, "Owner", 0, Gender.Female, PlayerStatus.Retained, team, TeamRole.Owner);
        NewPlayer(tournament, "Marquee", 0, Gender.Male, PlayerStatus.Retained, team, TeamRole.Marquee);

        var lot = NewPlayer(tournament, "On block", 5_000m, Gender.Male);
        NewPlayer(tournament, "Spare", 20_000m, Gender.Male);

        var standing = await StandingFor(tournament, team, lot);

        // Squad of 2 plus this win reaches the minimum of 3, and the woman requirement is
        // already met by the owner — so nothing has to be held back.
        Assert.Equal(0m, standing.ReservedPurse);
        Assert.Equal(100_000m, standing.SpendablePurse);
    }

    /// <summary>
    /// With a maximum squad size in play, signing a man can be refused outright when it would
    /// leave too few places for the women still owed.
    /// </summary>
    [Fact]
    public async Task A_man_is_refused_when_the_remaining_places_are_all_owed_to_women()
    {
        var tournament = NewTournament(minSquad: 4, maxSquad: 4, minWomen: 2, reserve: false);
        var team = NewTeam(tournament);

        NewPlayer(tournament, "Bought 1", 0, Gender.Male, PlayerStatus.Sold, team, soldPrice: 1_000m);
        NewPlayer(tournament, "Bought 2", 0, Gender.Male, PlayerStatus.Sold, team, soldPrice: 1_000m);

        var man = NewPlayer(tournament, "Another man", 1_000m, Gender.Male);
        var woman = NewPlayer(tournament, "A woman", 1_000m, Gender.Female);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AuctionRuleException>(
            () => _rules.EnsureBidAllowedAsync(tournament, team, man, 1_000m));

        Assert.Contains("must still sign 2 women", error.Message);

        // The same team may still bid on a woman.
        await _rules.EnsureBidAllowedAsync(tournament, team, woman, 1_000m);
    }

    [Fact]
    public async Task Turning_the_reserve_off_lets_a_team_spend_everything()
    {
        var tournament = NewTournament(minSquad: 5, minWomen: 3, reserve: false, purse: 50_000m);
        var team = NewTeam(tournament);
        var lot = NewPlayer(tournament, "On block", 1_000m, Gender.Male);
        NewPlayer(tournament, "Woman", 5_000m, Gender.Female);
        await _db.SaveChangesAsync();

        var standing = await StandingFor(tournament, team, lot);
        Assert.Equal(0m, standing.ReservedPurse);

        await _rules.EnsureBidAllowedAsync(tournament, team, lot, 50_000m);
    }
}
