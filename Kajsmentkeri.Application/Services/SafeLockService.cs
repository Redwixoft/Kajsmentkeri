using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kajsmentkeri.Application.Services;

public class SafeLockService : ISafeLockService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ITimeService _timeService;
    private readonly IPredictionService _predictionService;
    private readonly ILeaderboardService _leaderboardService;
    private readonly IServiceScopeFactory _scopeFactory;

    public SafeLockService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ITimeService timeService,
        IPredictionService predictionService,
        ILeaderboardService leaderboardService,
        IServiceScopeFactory scopeFactory)
    {
        _dbContextFactory = dbContextFactory;
        _timeService = timeService;
        _predictionService = predictionService;
        _leaderboardService = leaderboardService;
        _scopeFactory = scopeFactory;
    }

    public async Task<SafeLock?> GetSafeLockAsync(Guid matchId, Guid ownerUserId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.SafeLocks
            .FirstOrDefaultAsync(sl => sl.MatchId == matchId && sl.OwnerUserId == ownerUserId);
    }

    public async Task<HashSet<Guid>> GetOwnerIdsWithSafeLocksAsync(IEnumerable<Guid> matchIds)
    {
        var ids = matchIds.ToList();
        if (ids.Count == 0) return new HashSet<Guid>();

        using var context = _dbContextFactory.CreateDbContext();
        var ownerIds = await context.SafeLocks
            .Where(sl => ids.Contains(sl.MatchId))
            .Select(sl => sl.OwnerUserId)
            .ToListAsync();
        return ownerIds.ToHashSet();
    }

    public async Task SetSafeLockAsync(Guid matchId, Guid ownerUserId, Guid trackedUserId,
        int homeWinHome, int homeWinAway, int awayWinHome, int awayWinAway,
        int? drawHome, int? drawAway)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var match = await context.Matches
            .Include(m => m.Championship)
            .FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found.");

        // For the final match, require the bronze medal match to have been played first.
        // The bronze medal match itself has no such prerequisite.
        if (match.IsFinalMatch == true)
        {
            var bronzeMatch = await context.Matches
                .FirstOrDefaultAsync(m => m.ChampionshipId == match.ChampionshipId && m.IsBronzeMedalMatch == true);
            if (bronzeMatch != null && bronzeMatch.HomeScore == null)
                throw new InvalidOperationException("Safe lock on the final is only available after the bronze medal match has a result.");
        }

        // Rank check: tracked user must be ranked strictly better (lower rank number) than owner
        var leaderboard = await _leaderboardService.GetLeaderboardAsync(match.ChampionshipId);
        var ranks = ComputeRanks(leaderboard);

        if (!ranks.TryGetValue(ownerUserId, out var ownerRank))
            throw new InvalidOperationException("You must have predictions to set a safe lock.");
        if (!ranks.TryGetValue(trackedUserId, out var trackedRank))
            throw new InvalidOperationException("Tracked user is not on the leaderboard.");
        if (trackedRank >= ownerRank)
            throw new InvalidOperationException("You can only set a safe lock on a user ranked above you.");

        var existing = await context.SafeLocks
            .FirstOrDefaultAsync(sl => sl.MatchId == matchId && sl.OwnerUserId == ownerUserId);

        if (existing != null)
        {
            existing.TrackedUserId = trackedUserId;
            existing.HomeWinPredictedHome = homeWinHome;
            existing.HomeWinPredictedAway = homeWinAway;
            existing.AwayWinPredictedHome = awayWinHome;
            existing.AwayWinPredictedAway = awayWinAway;
            existing.DrawPredictedHome = drawHome;
            existing.DrawPredictedAway = drawAway;
        }
        else
        {
            context.SafeLocks.Add(new SafeLock
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                OwnerUserId = ownerUserId,
                TrackedUserId = trackedUserId,
                HomeWinPredictedHome = homeWinHome,
                HomeWinPredictedAway = homeWinAway,
                AwayWinPredictedHome = awayWinHome,
                AwayWinPredictedAway = awayWinAway,
                DrawPredictedHome = drawHome,
                DrawPredictedAway = drawAway,
                CreatedAt = _timeService.UtcNow
            });
        }

        // Resolve user names for audit log
        using var auditScope = _scopeFactory.CreateScope();
        var userManager = auditScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var ownerUser = await userManager.FindByIdAsync(ownerUserId.ToString());
        var trackedUser = await userManager.FindByIdAsync(trackedUserId.ToString());

        context.PredictionAuditLogs.Add(new PredictionAuditLog
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            AdminId = ownerUserId,
            AdminName = ownerUser?.UserName ?? "Unknown",
            TargetUserId = trackedUserId,
            TargetUserName = trackedUser?.UserName ?? "Unknown",
            NewHomeScore = 0,
            NewAwayScore = 0,
            TimestampUtc = _timeService.UtcNow,
            IsSafeLockCreated = true,
            MatchSummary = $"{match.HomeTeam} - {match.AwayTeam}"
        });

        await context.SaveChangesAsync();
    }

    public async Task RemoveSafeLockAsync(Guid matchId, Guid ownerUserId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var existing = await context.SafeLocks
            .Include(sl => sl.Match)
            .FirstOrDefaultAsync(sl => sl.MatchId == matchId && sl.OwnerUserId == ownerUserId);
        if (existing == null) return;

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var ownerUser = await userManager.FindByIdAsync(ownerUserId.ToString());
        var trackedUser = await userManager.FindByIdAsync(existing.TrackedUserId.ToString());

        context.SafeLocks.Remove(existing);
        context.PredictionAuditLogs.Add(new PredictionAuditLog
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            AdminId = ownerUserId,
            AdminName = ownerUser?.UserName ?? "Unknown",
            TargetUserId = existing.TrackedUserId,
            TargetUserName = trackedUser?.UserName ?? "Unknown",
            NewHomeScore = 0,
            NewAwayScore = 0,
            TimestampUtc = _timeService.UtcNow,
            IsSafeLockRemoved = true,
            MatchSummary = $"{existing.Match.HomeTeam} - {existing.Match.AwayTeam}"
        });
        await context.SaveChangesAsync();
    }

    public async Task EvaluateSafeLocksAsync(Guid matchId, Guid changedUserId,
        int? oldHome, int? oldAway, int newHome, int newAway)
    {
        var oldOutcome = oldHome.HasValue ? GetOutcome(oldHome.Value, oldAway!.Value) : (WinnerOutcome?)null;
        var newOutcome = GetOutcome(newHome, newAway);

        var queue = new Queue<(Guid UserId, WinnerOutcome? PrevOutcome, WinnerOutcome CurrOutcome)>();
        queue.Enqueue((changedUserId, oldOutcome, newOutcome));
        var visited = new HashSet<Guid>();

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        while (queue.Count > 0)
        {
            var (affectedUser, prevOutcome, currOutcome) = queue.Dequeue();
            if (!visited.Add(affectedUser)) continue;
            if (prevOutcome == currOutcome) continue;

            using var context = _dbContextFactory.CreateDbContext();
            var safeLocks = await context.SafeLocks
                .Where(sl => sl.MatchId == matchId && sl.TrackedUserId == affectedUser)
                .ToListAsync();

            if (safeLocks.Count == 0) continue;

            var match = await context.Matches.FirstAsync(m => m.Id == matchId);
            var trackedUser = await userManager.FindByIdAsync(affectedUser.ToString());
            var trackedUserName = trackedUser?.UserName ?? "Unknown";

            foreach (var sl in safeLocks)
            {
                (int targetHome, int targetAway) = currOutcome switch
                {
                    WinnerOutcome.Home => (sl.HomeWinPredictedHome, sl.HomeWinPredictedAway),
                    WinnerOutcome.Away => (sl.AwayWinPredictedHome, sl.AwayWinPredictedAway),
                    WinnerOutcome.Draw when sl.DrawPredictedHome.HasValue =>
                        (sl.DrawPredictedHome.Value, sl.DrawPredictedAway!.Value),
                    _ => (-1, -1) // Draw with no mapping configured — skip
                };
                if (targetHome < 0) continue;

                // Check if owner's lock time has passed
                var ownerLockTime = await _predictionService.GetPredictionLockTimeAsync(
                    match.ChampionshipId, matchId, sl.OwnerUserId);
                if (_timeService.UtcNow > ownerLockTime) continue;

                var ownerPrediction = await context.Predictions
                    .FirstOrDefaultAsync(p => p.MatchId == matchId && p.UserId == sl.OwnerUserId);

                var ownerOldOutcome = ownerPrediction != null
                    ? (WinnerOutcome?)GetOutcome(ownerPrediction.PredictedHome, ownerPrediction.PredictedAway)
                    : null;

                var ownerUser = await userManager.FindByIdAsync(sl.OwnerUserId.ToString());
                var ownerUserName = ownerUser?.UserName ?? "Unknown";

                int? oldH = ownerPrediction?.PredictedHome;
                int? oldA = ownerPrediction?.PredictedAway;

                if (ownerPrediction == null)
                {
                    ownerPrediction = new Prediction
                    {
                        Id = Guid.NewGuid(),
                        MatchId = matchId,
                        UserId = sl.OwnerUserId
                    };
                    context.Predictions.Add(ownerPrediction);
                }

                ownerPrediction.PredictedHome = targetHome;
                ownerPrediction.PredictedAway = targetAway;

                context.PredictionAuditLogs.Add(new PredictionAuditLog
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId,
                    AdminId = affectedUser,
                    AdminName = trackedUserName,
                    TargetUserId = sl.OwnerUserId,
                    TargetUserName = ownerUserName,
                    OldHomeScore = oldH,
                    OldAwayScore = oldA,
                    NewHomeScore = targetHome,
                    NewAwayScore = targetAway,
                    TimestampUtc = _timeService.UtcNow,
                    IsAdminUpdate = false,
                    IsRejected = false,
                    IsSafeLockTrigger = true,
                    MatchSummary = $"{match.HomeTeam} - {match.AwayTeam}"
                });

                sl.LastTriggeredAt = _timeService.UtcNow;
                await context.SaveChangesAsync();

                var newOwnerOutcome = GetOutcome(targetHome, targetAway);
                queue.Enqueue((sl.OwnerUserId, ownerOldOutcome, newOwnerOutcome));
            }
        }
    }

    public async Task ValidateSafeLocksAfterRescoreAsync(Guid championshipId)
    {
        var leaderboard = await _leaderboardService.GetLeaderboardAsync(championshipId);
        var ranks = ComputeRanks(leaderboard);

        using var context = _dbContextFactory.CreateDbContext();
        var invalidLocks = await context.SafeLocks
            .Include(sl => sl.Match)
            .Where(sl => sl.Match.ChampionshipId == championshipId && sl.Match.HomeScore == null)
            .ToListAsync();

        if (invalidLocks.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        foreach (var sl in invalidLocks)
        {
            var ownerRank = ranks.GetValueOrDefault(sl.OwnerUserId, int.MaxValue);
            var trackedRank = ranks.GetValueOrDefault(sl.TrackedUserId, int.MaxValue);

            // Invalid when tracked user is no longer strictly better-ranked than owner
            if (trackedRank >= ownerRank)
            {
                var ownerUser = await userManager.FindByIdAsync(sl.OwnerUserId.ToString());
                var trackedUser = await userManager.FindByIdAsync(sl.TrackedUserId.ToString());

                context.SafeLocks.Remove(sl);
                context.PredictionAuditLogs.Add(new PredictionAuditLog
                {
                    Id = Guid.NewGuid(),
                    MatchId = sl.MatchId,
                    AdminId = sl.OwnerUserId,
                    AdminName = ownerUser?.UserName ?? "Unknown",
                    TargetUserId = sl.TrackedUserId,
                    TargetUserName = trackedUser?.UserName ?? "Unknown",
                    NewHomeScore = 0,
                    NewAwayScore = 0,
                    TimestampUtc = _timeService.UtcNow,
                    IsSafeLockRemoved = true,
                    MatchSummary = $"{sl.Match.HomeTeam} - {sl.Match.AwayTeam}"
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static WinnerOutcome GetOutcome(int home, int away) =>
        home > away ? WinnerOutcome.Home :
        home < away ? WinnerOutcome.Away :
        WinnerOutcome.Draw;

    private static Dictionary<Guid, int> ComputeRanks(List<DTOs.LeaderboardEntryDto> leaderboard)
    {
        var ranks = new Dictionary<Guid, int>();
        int compRank = 1;
        for (int i = 0; i < leaderboard.Count; i++)
        {
            if (i > 0 && !AreTied(leaderboard[i], leaderboard[i - 1]))
                compRank = i + 1;
            ranks[leaderboard[i].UserId] = compRank;
        }
        return ranks;
    }

    private static bool AreTied(DTOs.LeaderboardEntryDto a, DTOs.LeaderboardEntryDto b) =>
        a.TotalPoints == b.TotalPoints &&
        a.CorrectWinners == b.CorrectWinners &&
        a.OneGoalMisses == b.OneGoalMisses &&
        a.OnlyCorrect == b.OnlyCorrect &&
        a.RarityPoints == b.RarityPoints;

    private enum WinnerOutcome { Home, Draw, Away }
}
