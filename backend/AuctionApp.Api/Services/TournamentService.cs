using AuctionApp.Api.Contracts;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;

namespace AuctionApp.Api.Services;

public interface ITournamentService
{
    Task<IReadOnlyList<SportDto>> ListSportsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TournamentDto>> ListAsync(CancellationToken ct = default);

    Task<TournamentDto?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Creates a tournament owned by <paramref name="ownerUserId"/>, its auctioneer.</summary>
    Task<TournamentDto> CreateAsync(SaveTournamentRequest request, int ownerUserId, CancellationToken ct = default);

    Task<TournamentDto> UpdateAsync(int id, SaveTournamentRequest request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<TeamSummaryDto>> ListTeamsAsync(int tournamentId, CancellationToken ct = default);

    Task<TeamSummaryDto> AddTeamAsync(int tournamentId, SaveTeamRequest request, CancellationToken ct = default);

    Task<TeamSummaryDto> UpdateTeamAsync(int teamId, SaveTeamRequest request, CancellationToken ct = default);

    Task RemoveTeamAsync(int teamId, CancellationToken ct = default);

    /// <summary>Adds a permanent squad member — the owner, or a marquee signing.</summary>
    Task<TeamSummaryDto> AddMemberAsync(int teamId, SaveTeamMemberRequest request, CancellationToken ct = default);

    Task<TeamSummaryDto> RemoveMemberAsync(int playerId, CancellationToken ct = default);
}

public class TournamentService : ITournamentService
{
    private readonly IUnitOfWork _uow;
    private readonly ISquadRuleService _rules;
    private readonly ITournamentAccess _access;

    public TournamentService(IUnitOfWork uow, ISquadRuleService rules, ITournamentAccess access)
    {
        _uow = uow;
        _rules = rules;
        _access = access;
    }

    public async Task<IReadOnlyList<SportDto>> ListSportsAsync(CancellationToken ct = default)
    {
        var sports = await _uow.Sports.ListWithTournamentCountsAsync(ct);
        return sports.Select(s => s.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<TournamentDto>> ListAsync(CancellationToken ct = default)
    {
        var tournaments = await _uow.Tournaments.ListWithSportAsync(ct);
        var result = new List<TournamentDto>(tournaments.Count);

        foreach (var tournament in tournaments)
        {
            var teams = await _uow.Teams.ListForTournamentAsync(tournament.Id, ct);
            var players = await _uow.Players.ListForTournamentAsync(tournament.Id, ct);
            result.Add(tournament.ToDto(teams.Count, players.Count, _access.CanManage(tournament)));
        }

        return result;
    }

    public async Task<TournamentDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var tournament = await _uow.Tournaments.GetWithDetailsAsync(id, ct);
        if (tournament is null) return null;

        var teams = await _uow.Teams.ListForTournamentAsync(id, ct);
        var players = await _uow.Players.ListForTournamentAsync(id, ct);
        return tournament.ToDto(teams.Count, players.Count, _access.CanManage(tournament));
    }

    public async Task<TournamentDto> CreateAsync(SaveTournamentRequest request, int ownerUserId, CancellationToken ct = default)
    {
        if (!await _uow.Sports.ExistsAsync(request.SportId, ct))
        {
            throw new NotFoundException($"Sport {request.SportId} was not found.");
        }

        Validate(request);

        var tournament = new Tournament
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            SportId = request.SportId,
            Type = request.Type,
            TeamPurse = request.TeamPurse,
            OwnerUserId = ownerUserId,
        };

        ApplyRules(tournament, request);

        await _uow.Tournaments.AddAsync(tournament, ct);
        await _uow.SaveChangesAsync(ct);

        var created = await _uow.Tournaments.GetWithDetailsAsync(tournament.Id, ct);
        return created!.ToDto(canManage: true);
    }

    public async Task<TournamentDto> UpdateAsync(int id, SaveTournamentRequest request, CancellationToken ct = default)
    {
        var tournament = await _uow.Tournaments.GetWithDetailsAsync(id, ct)
            ?? throw new NotFoundException($"Tournament {id} was not found.");

        var running = await _uow.Auctions.GetRunningForTournamentAsync(id, ct);
        if (running is not null)
        {
            throw new AuctionRuleException(
                $"'{running.Name}' is running. Rules cannot change mid-auction — close it first.");
        }

        Validate(request);

        tournament.Name = request.Name.Trim();
        tournament.Description = request.Description?.Trim();
        tournament.Type = request.Type;

        // Changing the purse only makes sense before anyone has spent from it, and the
        // auction cannot be running here, so pushing it to every team is safe.
        if (tournament.TeamPurse != request.TeamPurse)
        {
            tournament.TeamPurse = request.TeamPurse;
            foreach (var team in await _uow.Teams.ListForTournamentAsync(id, ct))
            {
                var tracked = await _uow.Teams.GetByIdAsync(team.Id, ct);
                if (tracked is null) continue;
                tracked.InitialPurse = request.TeamPurse;
                tracked.Purse = request.TeamPurse;
            }
        }

        ApplyRules(tournament, request);
        await _uow.SaveChangesAsync(ct);

        return (await GetAsync(id, ct))!;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var tournament = await _uow.Tournaments.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Tournament {id} was not found.");

        var running = await _uow.Auctions.GetRunningForTournamentAsync(id, ct);
        if (running is not null)
        {
            throw new AuctionRuleException($"Close '{running.Name}' before deleting this tournament.");
        }

        // Torn down in dependency order by hand. The database cascades tournament to
        // auctions and teams, but players hang off both and are deliberately NoAction, so
        // they have to go before the rows they point at.
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            var auctions = (await _uow.Auctions.ListWithTournamentAsync(token))
                .Where(a => a.TournamentId == id)
                .ToList();

            foreach (var auction in auctions)
            {
                var tracked = await _uow.Auctions.GetByIdAsync(auction.Id, token);
                if (tracked is not null)
                {
                    // Nulls CurrentPlayerId first: it points at a player about to be deleted.
                    tracked.CurrentPlayerId = null;
                }
            }

            await _uow.SaveChangesAsync(token);

            // Deleting the auctions cascades their bids and nulls Player.AuctionId.
            foreach (var auction in auctions)
            {
                var tracked = await _uow.Auctions.GetByIdAsync(auction.Id, token);
                if (tracked is not null) _uow.Auctions.Remove(tracked);
            }

            await _uow.SaveChangesAsync(token);

            foreach (var player in await _uow.Players.ListForTournamentAsync(id, token))
            {
                var tracked = await _uow.Players.GetByIdAsync(player.Id, token);
                if (tracked is not null) _uow.Players.Remove(tracked);
            }

            await _uow.SaveChangesAsync(token);

            // Teams cascade with the tournament itself.
            _uow.Tournaments.Remove(tournament);
            await _uow.SaveChangesAsync(token);
        },
        ct);
    }

    public async Task<IReadOnlyList<TeamSummaryDto>> ListTeamsAsync(int tournamentId, CancellationToken ct = default)
    {
        var tournament = await _uow.Tournaments.GetWithDetailsAsync(tournamentId, ct)
            ?? throw new NotFoundException($"Tournament {tournamentId} was not found.");

        return await _rules.BuildStandingsAsync(tournament, currentPlayer: null, ct);
    }

    public async Task<TeamSummaryDto> AddTeamAsync(
        int tournamentId,
        SaveTeamRequest request,
        CancellationToken ct = default)
    {
        var tournament = await _uow.Tournaments.GetWithDetailsAsync(tournamentId, ct)
            ?? throw new NotFoundException($"Tournament {tournamentId} was not found.");

        await EnsureOwnerIsValidAsync(request.OwnerUserId, ct);

        var team = new Team
        {
            TournamentId = tournamentId,
            Name = request.Name.Trim(),
            ShortName = request.ShortName?.Trim(),
            OwnerUserId = request.OwnerUserId,
            Purse = tournament.TeamPurse,
            InitialPurse = tournament.TeamPurse,
        };

        await _uow.Teams.AddAsync(team, ct);
        await _uow.SaveChangesAsync(ct);

        return await SummariseAsync(tournament, team.Id, ct);
    }

    public async Task<TeamSummaryDto> UpdateTeamAsync(
        int teamId,
        SaveTeamRequest request,
        CancellationToken ct = default)
    {
        var team = await _uow.Teams.GetByIdAsync(teamId, ct)
            ?? throw new NotFoundException($"Team {teamId} was not found.");

        await EnsureOwnerIsValidAsync(request.OwnerUserId, ct);

        team.Name = request.Name.Trim();
        team.ShortName = request.ShortName?.Trim();
        team.OwnerUserId = request.OwnerUserId;
        await _uow.SaveChangesAsync(ct);

        var tournament = await _uow.Tournaments.GetWithDetailsAsync(team.TournamentId, ct)!;
        return await SummariseAsync(tournament!, teamId, ct);
    }

    public async Task RemoveTeamAsync(int teamId, CancellationToken ct = default)
    {
        var team = await _uow.Teams.GetWithSquadAsync(teamId, ct)
            ?? throw new NotFoundException($"Team {teamId} was not found.");

        if (team.Players.Any(p => p.Status == PlayerStatus.Sold))
        {
            throw new AuctionRuleException(
                $"{team.Name} has already bought players. Reset the auction before removing the team.");
        }

        _uow.Teams.Remove(team);
        await _uow.SaveChangesAsync(ct);
    }

    /// <remarks>
    /// Created straight onto the squad: retained, priced at nothing, and outside the lot
    /// order. Nothing about them touches the purse, so adding one mid-auction is safe —
    /// though it does change the squad the rules are measured against.
    /// </remarks>
    public async Task<TeamSummaryDto> AddMemberAsync(
        int teamId,
        SaveTeamMemberRequest request,
        CancellationToken ct = default)
    {
        var team = await _uow.Teams.GetWithSquadAsync(teamId, ct)
            ?? throw new NotFoundException($"Team {teamId} was not found.");

        var tournament = await _uow.Tournaments.GetWithDetailsAsync(team.TournamentId, ct)
            ?? throw new NotFoundException($"Tournament {team.TournamentId} was not found.");

        await _access.RequireManageAsync(tournament.Id, ct);

        if (request.TeamRole == TeamRole.Auctioned)
        {
            throw new AuctionRuleException(
                "An auctioned player joins a team by being bought. Pick owner or marquee.");
        }

        // One owner per team; marquee signings are not capped here because the number of
        // them a league allows is a league decision, not a database one.
        if (request.TeamRole == TeamRole.Owner &&
            team.Players.Any(p => p.Status == PlayerStatus.Retained && p.TeamRole == TeamRole.Owner))
        {
            throw new AuctionRuleException($"{team.Name} already has an owner listed.");
        }

        var member = new Player
        {
            Name = request.Name.Trim(),
            TournamentId = tournament.Id,
            Role = request.Role?.Trim(),
            Gender = request.Gender,
            Country = request.Country?.Trim(),
            SkillsJson = SerialiseSkills(request.Skills),
            BasePrice = 0m,
            LotOrder = 0,
            Status = PlayerStatus.Retained,
            TeamRole = request.TeamRole,
            SoldToTeamId = team.Id,
        };

        await _uow.Players.AddAsync(member, ct);
        await _uow.SaveChangesAsync(ct);

        return await SummariseAsync(tournament, teamId, ct);
    }

    public async Task<TeamSummaryDto> RemoveMemberAsync(int playerId, CancellationToken ct = default)
    {
        var member = await _uow.Players.GetWithDetailsAsync(playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} was not found.");

        if (member.Status != PlayerStatus.Retained)
        {
            throw new AuctionRuleException("Only a retained member can be removed this way.");
        }

        var teamId = member.SoldToTeamId
            ?? throw new AuctionRuleException("That member is not attached to a team.");

        var tournament = await _uow.Tournaments.GetWithDetailsAsync(member.TournamentId, ct)
            ?? throw new NotFoundException($"Tournament {member.TournamentId} was not found.");

        await _access.RequireManageAsync(tournament.Id, ct);

        // Retained members never attract bids, but a stray row would block the delete.
        await _uow.Bids.DeleteForPlayerAsync(playerId, ct);
        _uow.Players.Remove(member);
        await _uow.SaveChangesAsync(ct);

        return await SummariseAsync(tournament, teamId, ct);
    }

    /// <summary>Blank values are dropped so an untouched form field does not become an empty skill.</summary>
    private static string? SerialiseSkills(Dictionary<string, string>? skills)
    {
        if (skills is null) return null;

        var filled = skills
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Trim());

        return filled.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(filled);
    }

    private async Task<TeamSummaryDto> SummariseAsync(Tournament tournament, int teamId, CancellationToken ct)
    {
        var standings = await _rules.BuildStandingsAsync(tournament, currentPlayer: null, ct);
        return standings.First(t => t.TeamId == teamId);
    }

    private async Task EnsureOwnerIsValidAsync(int? ownerUserId, CancellationToken ct)
    {
        if (ownerUserId is not int id) return;

        var owner = await _uow.Users.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"User {id} was not found.");

        if (owner.Role != UserRole.TeamOwner)
        {
            throw new AuctionRuleException("Only a team owner account can be assigned to a team.");
        }
    }

    private static void Validate(SaveTournamentRequest request)
    {
        if (request.MaxSquadSize > 0 && request.MinSquadSize > request.MaxSquadSize)
        {
            throw new AuctionRuleException("Minimum squad size cannot exceed the maximum.");
        }

        if (request.MaxSquadSize > 0 && request.MinWomenPerSquad > request.MaxSquadSize)
        {
            throw new AuctionRuleException(
                "More women are required than the squad can hold. Raise the maximum squad size.");
        }

        if (request.MinSquadSize > 0 && request.MinWomenPerSquad > request.MinSquadSize)
        {
            throw new AuctionRuleException(
                "More women are required than the minimum squad size. Raise the minimum squad size.");
        }
    }

    private static void ApplyRules(Tournament tournament, SaveTournamentRequest request)
    {
        tournament.MinSquadSize = request.MinSquadSize;
        tournament.MaxSquadSize = request.MaxSquadSize;
        tournament.MinWomenPerSquad = request.MinWomenPerSquad;
        tournament.MaxSpendPerPlayer = request.MaxSpendPerPlayer;
        tournament.ReservePurseForUnfilledSlots = request.ReservePurseForUnfilledSlots;
    }
}
