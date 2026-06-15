using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Application.Models;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Kajsmentkeri.Application.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;

    // Aggregations are event-invalidated (see InvalidateLeaderboard), so this long
    // duration is just a safety net against a missed invalidation path.
    private static readonly TimeSpan AggregateCacheDuration = TimeSpan.FromHours(6);

    private static readonly ChampionshipType?[] AllChampionshipTypeKeys =
        new ChampionshipType?[] { null }.Concat(Enum.GetValues<ChampionshipType>().Cast<ChampionshipType?>()).ToArray();

    private static string TypeKeyPart(ChampionshipType? type) => type?.ToString() ?? "all";

    private static string LeaderboardCacheKey(Guid championshipId) => $"leaderboard:{championshipId}";
    private static string StandingsCacheKey(Guid championshipId) => $"standings:{championshipId}";
    private static string AggregatesCacheKey(Guid championshipId) => $"aggregates:{championshipId}";
    private static string GlobalLeaderboardCacheKey(ChampionshipType? type) => $"globalLeaderboard:{TypeKeyPart(type)}";
    private static string GlobalStatsCacheKey(ChampionshipType? type) => $"globalStats:{TypeKeyPart(type)}";
    private static string ChampionshipRecordsCacheKey(ChampionshipType? type) => $"championshipRecords:{TypeKeyPart(type)}";
    private static string MedalCountsCacheKey(ChampionshipType? type) => $"medalCounts:{TypeKeyPart(type)}";

    public LeaderboardService(IServiceScopeFactory scopeFactory, IDbContextFactory<AppDbContext> dbContextFactory, IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
        _dbContextFactory = dbContextFactory;
        _cache = cache;
    }

    // Note: submitting/editing a prediction on a match with no result yet does not
    // change any points, so it intentionally does NOT call this method — the new
    // zero-point row is merged in at the page layer (Championship Details) and
    // global stats ignore unscored predictions anyway.
    public void InvalidateLeaderboard(Guid championshipId)
    {
        _cache.Remove(LeaderboardCacheKey(championshipId));
        _cache.Remove(StandingsCacheKey(championshipId));
        _cache.Remove(AggregatesCacheKey(championshipId));

        // Global aggregations span every championship, so a result/recalculation in any
        // one of them can change the totals — clear all type variants rather than
        // tracking fine-grained per-championship dependencies.
        foreach (var type in AllChampionshipTypeKeys)
        {
            _cache.Remove(GlobalLeaderboardCacheKey(type));
            _cache.Remove(GlobalStatsCacheKey(type));
            _cache.Remove(ChampionshipRecordsCacheKey(type));
            _cache.Remove(MedalCountsCacheKey(type));
        }
    }

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(Guid championshipId)
    {
        return await _cache.GetOrCreateAsync(LeaderboardCacheKey(championshipId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = AggregateCacheDuration;
            return await BuildLeaderboardAsync(championshipId);
        }) ?? new List<LeaderboardEntryDto>();
    }

    private async Task<List<LeaderboardEntryDto>> BuildLeaderboardAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();

        // Single scan: fetch the per-prediction fields needed for both the aggregate stats
        // and the OnlyOneTries computation, instead of querying Predictions twice.
        var predictions = await context.Predictions
            .Where(p => p.Match.ChampionshipId == championshipId)
            .Select(p => new
            {
                p.UserId,
                p.MatchId,
                p.Points,
                p.GotWinner,
                p.OneGoalMiss,
                p.IsOnlyCorrect,
                p.GotExactScore,
                p.RarityPart,
                p.PredictedHome,
                p.PredictedAway,
                IsScored = p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue
            })
            .ToListAsync();

        var grouped = predictions
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(p => p.Points),
                CorrectWinners = g.Count(p => p.GotWinner),
                OneGoalMisses = g.Count(p => p.OneGoalMiss),
                OnlyCorrect = g.Count(p => p.IsOnlyCorrect),
                ExactScores = g.Count(p => p.GotExactScore),
                RarityPoints = g.Sum(p => p.RarityPart)
            })
            .ToList();

        var winnerPoints = await context.ChampionshipWinnerPredictions
            .Where(p => p.ChampionshipId == championshipId && p.PointsAwarded.HasValue)
            .ToDictionaryAsync(p => p.UserId, p => p.PointsAwarded!.Value);

        // OnlyOneTries: scored matches where the user was the sole predictor of their chosen outcome
        var onlyOneTries = predictions
            .Where(p => p.IsScored)
            .GroupBy(p => new { p.MatchId, Winner = p.PredictedHome > p.PredictedAway ? "H" : p.PredictedHome < p.PredictedAway ? "A" : "D" })
            .Where(g => g.Count() == 1)
            .SelectMany(g => g)
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Load all relevant users
        var userIds = grouped.Select(g => g.UserId).ToList();

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var users = await userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName);

        // Merge results
        var leaderboard = grouped.Select(g => new LeaderboardEntryDto
            {
                UserId = g.UserId,
                UserName = users.TryGetValue(g.UserId, out var name) ? name ?? "???" : "Unknown",
                TotalPoints = g.TotalPoints + winnerPoints.GetValueOrDefault(g.UserId, 0),
                CorrectWinners = g.CorrectWinners,
                OneGoalMisses = g.OneGoalMisses,
                OnlyCorrect = g.OnlyCorrect,
                OnlyOneTries = onlyOneTries.GetValueOrDefault(g.UserId, 0),
                ExactScores = g.ExactScores,
                WinnerPredictionPoints = winnerPoints.GetValueOrDefault(g.UserId, 0),
                RarityPoints = g.RarityPoints
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.CorrectWinners)
            .ThenByDescending(x => x.OneGoalMisses)
            .ThenByDescending(x => x.OnlyCorrect)
            .ThenByDescending(x => x.RarityPoints)
            .ToList();

        return leaderboard;
    }

    public async Task<List<LeaderboardEntryDto>> GetGlobalLeaderboardAsync(ChampionshipType? type = null)
    {
        return await _cache.GetOrCreateAsync(GlobalLeaderboardCacheKey(type), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = AggregateCacheDuration;
            return await BuildGlobalLeaderboardAsync(type);
        }) ?? new List<LeaderboardEntryDto>();
    }

    private async Task<List<LeaderboardEntryDto>> BuildGlobalLeaderboardAsync(ChampionshipType? type)
    {
        using var context = _dbContextFactory.CreateDbContext();

        // Single projected scan over all predictions, carrying everything needed for
        // both the per-user aggregates and the OnlyOneTries computation below.
        var predictionData = await context.Predictions
            .Where(p => !p.Match.Championship.IsTest)
            .Where(p => type == null || p.Match.Championship.Type == type)
            .Select(p => new
            {
                p.UserId,
                p.MatchId,
                p.Points,
                p.GotWinner,
                p.OneGoalMiss,
                p.IsOnlyCorrect,
                p.GotExactScore,
                p.RarityPart,
                p.Match.ChampionshipId,
                p.PredictedHome,
                p.PredictedAway,
                IsScored = p.Match.HomeScore.HasValue
            })
            .ToListAsync();

        var grouped = predictionData
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(p => p.Points),
                CorrectWinners = g.Count(p => p.GotWinner),
                OneGoalMisses = g.Count(p => p.OneGoalMiss),
                OnlyCorrect = g.Count(p => p.IsOnlyCorrect),
                ExactScores = g.Count(p => p.GotExactScore),
                RarityPoints = g.Sum(p => p.RarityPart),
                TotalPredictions = g.Count(),
                TotalScoredPredictions = g.Count(p => p.IsScored),
                ChampionshipCount = g.Select(p => p.ChampionshipId).Distinct().Count()
            })
            .ToList();

        var winnerPointsGlobal = await context.ChampionshipWinnerPredictions
            .Where(p => !p.Championship.IsTest && p.PointsAwarded.HasValue)
            .Where(p => type == null || p.Championship.Type == type)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(p => p.PointsAwarded!.Value) })
            .ToDictionaryAsync(p => p.UserId, p => p.Points);

        // OnlyOneTries: scored matches where the user was the sole predictor of their chosen outcome
        var onlyOneTries = predictionData
            .Where(p => p.IsScored)
            .GroupBy(p => new { p.MatchId, Winner = p.PredictedHome > p.PredictedAway ? "H" : p.PredictedHome < p.PredictedAway ? "A" : "D" })
            .Where(g => g.Count() == 1)
            .SelectMany(g => g)
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Load all relevant users
        var userIds = grouped.Select(g => g.UserId).ToList();

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var users = await userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName);

        // Merge results
        var leaderboard = grouped.Select(g => new LeaderboardEntryDto
            {
                UserId = g.UserId,
                UserName = users.TryGetValue(g.UserId, out var name) ? name ?? "???" : "Unknown",
                TotalPoints = g.TotalPoints + winnerPointsGlobal.GetValueOrDefault(g.UserId, 0),
                CorrectWinners = g.CorrectWinners,
                OneGoalMisses = g.OneGoalMisses,
                OnlyCorrect = g.OnlyCorrect,
                OnlyOneTries = onlyOneTries.GetValueOrDefault(g.UserId, 0),
                ExactScores = g.ExactScores,
                WinnerPredictionPoints = winnerPointsGlobal.GetValueOrDefault(g.UserId, 0),
                RarityPoints = g.RarityPoints,
                ChampionshipCount = g.ChampionshipCount,
                TotalPredictions = g.TotalPredictions,
                TotalScoredPredictions = g.TotalScoredPredictions
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.CorrectWinners)
            .ThenByDescending(x => x.OneGoalMisses)
            .ThenByDescending(x => x.OnlyCorrect)
            .ThenByDescending(x => x.RarityPoints)
            .ToList();

        return leaderboard;
    }

    public async Task<ChampionshipRecordsDto> GetChampionshipRecordsAsync(ChampionshipType? type = null)
    {
        return await _cache.GetOrCreateAsync(ChampionshipRecordsCacheKey(type), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = AggregateCacheDuration;
            return await BuildChampionshipRecordsAsync(type);
        }) ?? new ChampionshipRecordsDto();
    }

    private async Task<ChampionshipRecordsDto> BuildChampionshipRecordsAsync(ChampionshipType? type)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var championships = await context.Championships
            .Where(c => !c.IsTest)
            .Where(c => type == null || c.Type == type)
            .Select(c => new { c.Id, c.Name, c.Year })
            .ToListAsync();

        if (championships.Count == 0)
            return new ChampionshipRecordsDto();

        var championshipIds = championships.Select(c => c.Id).ToList();

        var rawStats = await context.Predictions
            .Where(p => championshipIds.Contains(p.Match.ChampionshipId))
            .GroupBy(p => new { p.Match.ChampionshipId, p.UserId })
            .Select(g => new
            {
                ChampionshipId = g.Key.ChampionshipId,
                UserId = g.Key.UserId,
                Points = g.Sum(p => p.Points),
                Winners = g.Count(p => p.GotWinner),
                Misses = g.Count(p => p.OneGoalMiss),
                Luckers = g.Count(p => p.GotExactScore),
                OnlyOnes = g.Count(p => p.IsOnlyCorrect),
                RarityPoints = g.Sum(p => p.RarityPart)
            })
            .ToListAsync();

        var winnerPointsList = await context.ChampionshipWinnerPredictions
            .Where(p => championshipIds.Contains(p.ChampionshipId) && p.PointsAwarded.HasValue)
            .Select(p => new { p.ChampionshipId, p.UserId, Points = p.PointsAwarded!.Value })
            .ToListAsync();

        var winnerPointsLookup = winnerPointsList
            .ToDictionary(p => (p.ChampionshipId, p.UserId), p => p.Points);

        var userIds = rawStats.Select(p => p.UserId).Distinct().ToList();
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var userNames = await userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "?");

        var champLookup = championships.ToDictionary(c => c.Id);

        var entries = rawStats.Select(p => (
            ChampionshipId: p.ChampionshipId,
            UserName: userNames.GetValueOrDefault(p.UserId, "?"),
            TotalPoints: p.Points + winnerPointsLookup.GetValueOrDefault((p.ChampionshipId, p.UserId), 0),
            Winners: p.Winners,
            Misses: p.Misses,
            Luckers: p.Luckers,
            OnlyOnes: p.OnlyOnes,
            RarityPoints: p.RarityPoints
        )).ToList();

        RecordEntryDto ToDto(Guid champId, string userName, int value) => new()
        {
            UserName = userName,
            Value = value,
            ChampionshipId = champId,
            ChampionshipName = champLookup[champId].Name,
            ChampionshipYear = champLookup[champId].Year
        };

        List<RecordEntryDto> MaxRecord(int max, Func<(Guid ChampionshipId, string UserName, int TotalPoints, int Winners, int Misses, int Luckers, int OnlyOnes, decimal RarityPoints), int> getValue) =>
            entries.Where(e => getValue(e) == max).Select(e => ToDto(e.ChampionshipId, e.UserName, max)).ToList();

        var maxPoints   = entries.Count > 0 ? entries.Max(e => e.TotalPoints) : 0;
        var maxWinners  = entries.Count > 0 ? entries.Max(e => e.Winners)     : 0;
        var maxMisses   = entries.Count > 0 ? entries.Max(e => e.Misses)      : 0;
        var maxLuckers  = entries.Count > 0 ? entries.Max(e => e.Luckers)     : 0;
        var maxOnlyOnes = entries.Count > 0 ? entries.Max(e => e.OnlyOnes)    : 0;

        // Highest point gap between 1st and 2nd per championship
        int maxGap = 0;
        var gapRecords = new List<RecordEntryDto>();
        foreach (var champ in championships)
        {
            var ranked = entries
                .Where(e => e.ChampionshipId == champ.Id)
                .OrderByDescending(e => e.TotalPoints)
                .ThenByDescending(e => e.Winners)
                .ThenByDescending(e => e.Misses)
                .ThenByDescending(e => e.OnlyOnes)
                .ThenByDescending(e => e.RarityPoints)
                .ToList();

            if (ranked.Count >= 2)
            {
                int gap = ranked[0].TotalPoints - ranked[1].TotalPoints;
                if (gap > maxGap)
                {
                    maxGap = gap;
                    gapRecords = [ToDto(champ.Id, ranked[0].UserName, gap)];
                }
                else if (gap == maxGap && maxGap > 0)
                {
                    gapRecords.Add(ToDto(champ.Id, ranked[0].UserName, gap));
                }
            }
        }

        // Smallest point gap between 1st and 2nd per championship
        int minGap = int.MaxValue;
        var smallestGapRecords = new List<RecordEntryDto>();
        foreach (var champ in championships)
        {
            var ranked = entries
                .Where(e => e.ChampionshipId == champ.Id)
                .OrderByDescending(e => e.TotalPoints)
                .ThenByDescending(e => e.Winners)
                .ThenByDescending(e => e.Misses)
                .ThenByDescending(e => e.OnlyOnes)
                .ThenByDescending(e => e.RarityPoints)
                .ToList();

            if (ranked.Count >= 2)
            {
                int gap = ranked[0].TotalPoints - ranked[1].TotalPoints;
                if (gap < minGap)
                {
                    minGap = gap;
                    smallestGapRecords = [ToDto(champ.Id, ranked[0].UserName, gap)];
                }
                else if (gap == minGap)
                {
                    smallestGapRecords.Add(ToDto(champ.Id, ranked[0].UserName, gap));
                }
            }
        }

        // Streak records: longest consecutive correct/incorrect winner predictions per (championship, user)
        var scoredPredictionDetails = await context.Predictions
            .Where(p => championshipIds.Contains(p.Match.ChampionshipId)
                     && p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue)
            .Select(p => new { p.Match.ChampionshipId, p.UserId, p.GotWinner, p.Match.StartTimeUtc })
            .ToListAsync();

        var streakEntries = scoredPredictionDetails
            .GroupBy(p => (p.ChampionshipId, p.UserId))
            .Select(g =>
            {
                int maxPos = 0, maxNeg = 0, curPos = 0, curNeg = 0;
                foreach (var pred in g.OrderBy(p => p.StartTimeUtc))
                {
                    if (pred.GotWinner) { curPos++; curNeg = 0; if (curPos > maxPos) maxPos = curPos; }
                    else                { curNeg++; curPos = 0; if (curNeg > maxNeg) maxNeg = curNeg; }
                }
                return (g.Key.ChampionshipId, g.Key.UserId, MaxPos: maxPos, MaxNeg: maxNeg);
            })
            .ToList();

        int maxPosStreak = streakEntries.Count > 0 ? streakEntries.Max(e => e.MaxPos) : 0;
        int maxNegStreak = streakEntries.Count > 0 ? streakEntries.Max(e => e.MaxNeg) : 0;

        var posStreakRecords = maxPosStreak > 0
            ? streakEntries
                .Where(e => e.MaxPos == maxPosStreak)
                .Select(e => ToDto(e.ChampionshipId, userNames.GetValueOrDefault(e.UserId, "?"), maxPosStreak))
                .ToList()
            : new List<RecordEntryDto>();

        var negStreakRecords = maxNegStreak > 0
            ? streakEntries
                .Where(e => e.MaxNeg == maxNegStreak)
                .Select(e => ToDto(e.ChampionshipId, userNames.GetValueOrDefault(e.UserId, "?"), maxNegStreak))
                .ToList()
            : new List<RecordEntryDto>();

        return new ChampionshipRecordsDto
        {
            MostPoints            = maxPoints   > 0 ? MaxRecord(maxPoints,   e => e.TotalPoints) : [],
            MostWinners           = maxWinners  > 0 ? MaxRecord(maxWinners,  e => e.Winners)     : [],
            MostOneGoalMisses     = maxMisses   > 0 ? MaxRecord(maxMisses,   e => e.Misses)      : [],
            MostLuckers           = maxLuckers  > 0 ? MaxRecord(maxLuckers,  e => e.Luckers)     : [],
            MostOnlyOnes          = maxOnlyOnes > 0 ? MaxRecord(maxOnlyOnes, e => e.OnlyOnes)    : [],
            HighestPointGap       = gapRecords,
            SmallestPointGap      = minGap < int.MaxValue ? smallestGapRecords : [],
            LongestPositiveStreak = posStreakRecords,
            LongestNegativeStreak = negStreakRecords
        };
    }

    public async Task<Dictionary<Guid, List<(int Position, string ChampionshipName, int Year)>>> GetMedalCountsAsync(ChampionshipType? type = null)
    {
        return await _cache.GetOrCreateAsync(MedalCountsCacheKey(type), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = AggregateCacheDuration;
            return await BuildMedalCountsAsync(type);
        }) ?? new Dictionary<Guid, List<(int Position, string ChampionshipName, int Year)>>();
    }

    private async Task<Dictionary<Guid, List<(int Position, string ChampionshipName, int Year)>>> BuildMedalCountsAsync(ChampionshipType? type)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var qualifyingChampionships = await context.Championships
            .Where(c => !c.IsTest && (c.IsChampionshipEnded || c.Year < 2026))
            .Where(c => type == null || c.Type == type)
            .Select(c => new { c.Id, c.Name, c.Year })
            .ToListAsync();

        if (qualifyingChampionships.Count == 0)
            return new Dictionary<Guid, List<(int, string, int)>>();

        var qualifyingIds = qualifyingChampionships.Select(c => c.Id).ToList();

        var predictions = await context.Predictions
            .Where(p => qualifyingIds.Contains(p.Match.ChampionshipId))
            .GroupBy(p => new { p.Match.ChampionshipId, p.UserId })
            .Select(g => new
            {
                ChampionshipId = g.Key.ChampionshipId,
                UserId = g.Key.UserId,
                TotalPoints = g.Sum(p => p.Points),
                CorrectWinners = g.Count(p => p.GotWinner),
                OneGoalMisses = g.Count(p => p.OneGoalMiss),
                OnlyCorrect = g.Count(p => p.IsOnlyCorrect),
                RarityPoints = g.Sum(p => p.RarityPart)
            })
            .ToListAsync();

        var winnerPoints = await context.ChampionshipWinnerPredictions
            .Where(p => qualifyingIds.Contains(p.ChampionshipId) && p.PointsAwarded.HasValue)
            .Select(p => new { p.ChampionshipId, p.UserId, Points = p.PointsAwarded!.Value })
            .ToListAsync();

        var winnerPointsLookup = winnerPoints
            .ToDictionary(p => (p.ChampionshipId, p.UserId), p => p.Points);

        var medals = new Dictionary<Guid, List<(int Position, string ChampionshipName, int Year)>>();

        foreach (var championship in qualifyingChampionships)
        {
            var ranked = predictions
                .Where(p => p.ChampionshipId == championship.Id)
                .Select(p => new
                {
                    p.UserId,
                    Total = p.TotalPoints + winnerPointsLookup.GetValueOrDefault((championship.Id, p.UserId), 0),
                    p.CorrectWinners,
                    p.OneGoalMisses,
                    p.OnlyCorrect,
                    p.RarityPoints
                })
                .OrderByDescending(e => e.Total)
                .ThenByDescending(e => e.CorrectWinners)
                .ThenByDescending(e => e.OneGoalMisses)
                .ThenByDescending(e => e.OnlyCorrect)
                .ThenByDescending(e => e.RarityPoints)
                .ToList();

            // Assign competition positions (1, 1, 3 — not 1, 2, 3) using all tiebreakers
            int champPos = 1;
            for (int i = 0; i < ranked.Count; i++)
            {
                if (i > 0)
                {
                    var prev = ranked[i - 1];
                    var curr = ranked[i];
                    if (curr.Total != prev.Total || curr.CorrectWinners != prev.CorrectWinners ||
                        curr.OneGoalMisses != prev.OneGoalMisses || curr.OnlyCorrect != prev.OnlyCorrect ||
                        curr.RarityPoints != prev.RarityPoints)
                    {
                        champPos = i + 1;
                    }
                }

                if (champPos <= 3)
                {
                    var userId = ranked[i].UserId;
                    if (!medals.TryGetValue(userId, out var list))
                        medals[userId] = list = new List<(int, string, int)>();
                    list.Add((champPos, championship.Name, championship.Year));
                }
            }

            // Last place: all users tied at the highest position number, only when that position > 3
            if (ranked.Count > 0)
            {
                int maxPos = champPos; // champPos ends on the last group's position
                if (maxPos > 3)
                {
                    for (int i = ranked.Count - 1; i >= 0; i--)
                    {
                        // Walk back through all users sharing the last position
                        var curr = ranked[i];
                        var next = i + 1 < ranked.Count ? ranked[i + 1] : null;
                        if (next != null &&
                            (curr.Total != next.Total || curr.CorrectWinners != next.CorrectWinners ||
                             curr.OneGoalMisses != next.OneGoalMisses || curr.OnlyCorrect != next.OnlyCorrect ||
                             curr.RarityPoints != next.RarityPoints))
                            break;

                        var userId = curr.UserId;
                        if (!medals.TryGetValue(userId, out var lastList))
                            medals[userId] = lastList = new List<(int, string, int)>();
                        lastList.Add((ranked.Count, championship.Name, championship.Year));
                    }
                }
            }
        }

        return medals;
    }

    public async Task<Dictionary<Guid, string?>> GetChampionshipWinnersAsync(IEnumerable<Guid> championshipIds)
    {
        var ids = championshipIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string?>();

        using var context = _dbContextFactory.CreateDbContext();

        var scores = await context.Predictions
            .Where(p => ids.Contains(p.Match.ChampionshipId))
            .GroupBy(p => new { p.Match.ChampionshipId, p.UserId })
            .Select(g => new { ChampionshipId = g.Key.ChampionshipId, UserId = g.Key.UserId, Points = g.Sum(p => p.Points) })
            .ToListAsync();

        var winnerPredPoints = await context.ChampionshipWinnerPredictions
            .Where(p => ids.Contains(p.ChampionshipId) && p.PointsAwarded.HasValue)
            .Select(p => new { p.ChampionshipId, p.UserId, Points = p.PointsAwarded!.Value })
            .ToListAsync();

        var winnerPredLookup = winnerPredPoints.ToDictionary(p => (p.ChampionshipId, p.UserId), p => p.Points);

        var winnerUserIds = new Dictionary<Guid, Guid>();
        foreach (var champId in ids)
        {
            var top = scores
                .Where(s => s.ChampionshipId == champId)
                .Select(s => new { s.UserId, Total = s.Points + winnerPredLookup.GetValueOrDefault((champId, s.UserId), 0) })
                .OrderByDescending(s => s.Total)
                .FirstOrDefault();

            if (top != null)
                winnerUserIds[champId] = top.UserId;
        }

        if (winnerUserIds.Count == 0)
            return ids.ToDictionary(id => id, id => (string?)null);

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var userNames = await userManager.Users
            .Where(u => winnerUserIds.Values.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName);

        return ids.ToDictionary(
            id => id,
            id => winnerUserIds.TryGetValue(id, out var userId) && userNames.TryGetValue(userId, out var name)
                ? name
                : null);
    }

    public async Task<Dictionary<Guid, List<(string UserName, int Position)>>> GetChampionshipStandingsAsync(
        IEnumerable<Guid> championshipIds, Guid? currentUserId = null, int topN = 3)
    {
        var ids = championshipIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, List<(string UserName, int Position)>>();

        // The ranked (UserId, Position) list per championship is user-independent and
        // cached individually so it survives across requests/users until invalidated.
        var rankedByChamp = new Dictionary<Guid, List<(Guid UserId, int Position)>>();
        var missingIds = new List<Guid>();
        foreach (var id in ids)
        {
            if (_cache.TryGetValue(StandingsCacheKey(id), out List<(Guid UserId, int Position)>? cached) && cached != null)
                rankedByChamp[id] = cached;
            else
                missingIds.Add(id);
        }

        if (missingIds.Count > 0)
        {
            using var context = _dbContextFactory.CreateDbContext();

            var scores = await context.Predictions
                .Where(p => missingIds.Contains(p.Match.ChampionshipId))
                .GroupBy(p => new { p.Match.ChampionshipId, p.UserId })
                .Select(g => new { ChampionshipId = g.Key.ChampionshipId, UserId = g.Key.UserId, Points = g.Sum(p => p.Points) })
                .ToListAsync();

            var winnerPredPoints = await context.ChampionshipWinnerPredictions
                .Where(p => missingIds.Contains(p.ChampionshipId) && p.PointsAwarded.HasValue)
                .Select(p => new { p.ChampionshipId, p.UserId, Points = p.PointsAwarded!.Value })
                .ToListAsync();

            var winnerPredLookup = winnerPredPoints.ToDictionary(p => (p.ChampionshipId, p.UserId), p => p.Points);

            // Compute tied ranks per championship
            foreach (var champId in missingIds)
            {
                var ordered = scores
                    .Where(s => s.ChampionshipId == champId)
                    .Select(s => new { s.UserId, Total = s.Points + winnerPredLookup.GetValueOrDefault((champId, s.UserId), 0) })
                    .OrderByDescending(s => s.Total)
                    .ToList();

                var ranked = new List<(Guid UserId, int Position)>();
                int compRank = 1;
                for (int i = 0; i < ordered.Count; i++)
                {
                    if (i > 0 && ordered[i].Total != ordered[i - 1].Total)
                        compRank = i + 1;
                    ranked.Add((ordered[i].UserId, compRank));
                }

                rankedByChamp[champId] = ranked;
                _cache.Set(StandingsCacheKey(champId), ranked, AggregateCacheDuration);
            }
        }

        // Collect user IDs to load: all users at rank <= topN, plus the current user
        var allRelevantUserIds = rankedByChamp.Values
            .SelectMany(list => list.Where(r => r.Position <= topN).Select(r => r.UserId))
            .ToHashSet();
        if (currentUserId.HasValue)
            allRelevantUserIds.Add(currentUserId.Value);

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var userNames = await userManager.Users
            .Where(u => allRelevantUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Id.ToString());

        var result = new Dictionary<Guid, List<(string UserName, int Position)>>();
        foreach (var champId in ids)
        {
            var ranked = rankedByChamp.TryGetValue(champId, out var r) ? r : new List<(Guid UserId, int Position)>();
            var entries = new List<(string UserName, int Position)>();

            foreach (var (userId, position) in ranked.Where(r => r.Position <= topN))
            {
                if (userNames.TryGetValue(userId, out var name))
                    entries.Add((name, position));
            }

            if (currentUserId.HasValue)
            {
                var currentEntry = ranked.FirstOrDefault(r => r.UserId == currentUserId.Value);
                if (currentEntry.UserId == currentUserId.Value && currentEntry.Position > topN
                    && userNames.TryGetValue(currentUserId.Value, out var currentName))
                    entries.Add((currentName, currentEntry.Position));
            }

            result[champId] = entries;
        }

        return result;
    }

    public async Task<ChampionshipRecordsDto> GetUserPersonalRecordsAsync(Guid userId, ChampionshipType? type = null)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var championships = await context.Championships
            .Where(c => !c.IsTest)
            .Where(c => type == null || c.Type == type)
            .Select(c => new { c.Id, c.Name, c.Year })
            .ToListAsync();

        if (championships.Count == 0)
            return new ChampionshipRecordsDto();

        var championshipIds = championships.Select(c => c.Id).ToList();

        // Single projected scan over this user's predictions in these championships,
        // carrying both the per-championship totals and the scored-match data for streaks.
        var userPredictions = await context.Predictions
            .Where(p => championshipIds.Contains(p.Match.ChampionshipId) && p.UserId == userId)
            .Select(p => new
            {
                p.Match.ChampionshipId,
                p.Points,
                p.GotWinner,
                p.OneGoalMiss,
                p.GotExactScore,
                p.IsOnlyCorrect,
                IsScored = p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue,
                p.Match.StartTimeUtc
            })
            .ToListAsync();

        var rawStats = userPredictions
            .GroupBy(p => p.ChampionshipId)
            .Select(g => new
            {
                ChampionshipId = g.Key,
                Points = g.Sum(p => p.Points),
                Winners = g.Count(p => p.GotWinner),
                Misses = g.Count(p => p.OneGoalMiss),
                Luckers = g.Count(p => p.GotExactScore),
                OnlyOnes = g.Count(p => p.IsOnlyCorrect)
            })
            .ToList();

        if (rawStats.Count == 0)
            return new ChampionshipRecordsDto();

        var winnerPoints = await context.ChampionshipWinnerPredictions
            .Where(p => championshipIds.Contains(p.ChampionshipId) && p.UserId == userId && p.PointsAwarded.HasValue)
            .Select(p => new { p.ChampionshipId, Points = p.PointsAwarded!.Value })
            .ToDictionaryAsync(p => p.ChampionshipId, p => p.Points);

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        var userName = user?.UserName ?? "?";

        var champLookup = championships.ToDictionary(c => c.Id);

        var entries = rawStats.Select(p => (
            ChampionshipId: p.ChampionshipId,
            TotalPoints: p.Points + winnerPoints.GetValueOrDefault(p.ChampionshipId, 0),
            Winners: p.Winners,
            Misses: p.Misses,
            Luckers: p.Luckers,
            OnlyOnes: p.OnlyOnes
        )).ToList();

        RecordEntryDto ToDto(Guid champId, int value) => new()
        {
            UserName = userName,
            Value = value,
            ChampionshipId = champId,
            ChampionshipName = champLookup[champId].Name,
            ChampionshipYear = champLookup[champId].Year
        };

        List<RecordEntryDto> MaxRecord(int max, Func<(Guid ChampionshipId, int TotalPoints, int Winners, int Misses, int Luckers, int OnlyOnes), int> getValue) =>
            entries.Where(e => getValue(e) == max).Select(e => ToDto(e.ChampionshipId, max)).ToList();

        var maxPoints   = entries.Max(e => e.TotalPoints);
        var maxWinners  = entries.Max(e => e.Winners);
        var maxMisses   = entries.Max(e => e.Misses);
        var maxLuckers  = entries.Max(e => e.Luckers);
        var maxOnlyOnes = entries.Max(e => e.OnlyOnes);

        var scoredPredictions = userPredictions.Where(p => p.IsScored).ToList();

        var streakEntries = scoredPredictions
            .GroupBy(p => p.ChampionshipId)
            .Select(g =>
            {
                int maxPos = 0, maxNeg = 0, curPos = 0, curNeg = 0;
                foreach (var pred in g.OrderBy(p => p.StartTimeUtc))
                {
                    if (pred.GotWinner) { curPos++; curNeg = 0; if (curPos > maxPos) maxPos = curPos; }
                    else                { curNeg++; curPos = 0; if (curNeg > maxNeg) maxNeg = curNeg; }
                }
                return (ChampionshipId: g.Key, MaxPos: maxPos, MaxNeg: maxNeg);
            })
            .ToList();

        int maxPosStreak = streakEntries.Count > 0 ? streakEntries.Max(e => e.MaxPos) : 0;
        int maxNegStreak = streakEntries.Count > 0 ? streakEntries.Max(e => e.MaxNeg) : 0;

        var posStreakRecords = maxPosStreak > 0
            ? streakEntries.Where(e => e.MaxPos == maxPosStreak).Select(e => ToDto(e.ChampionshipId, maxPosStreak)).ToList()
            : new List<RecordEntryDto>();

        var negStreakRecords = maxNegStreak > 0
            ? streakEntries.Where(e => e.MaxNeg == maxNegStreak).Select(e => ToDto(e.ChampionshipId, maxNegStreak)).ToList()
            : new List<RecordEntryDto>();

        return new ChampionshipRecordsDto
        {
            MostPoints            = maxPoints   > 0 ? MaxRecord(maxPoints,   e => e.TotalPoints) : [],
            MostWinners           = maxWinners  > 0 ? MaxRecord(maxWinners,  e => e.Winners)     : [],
            MostOneGoalMisses     = maxMisses   > 0 ? MaxRecord(maxMisses,   e => e.Misses)      : [],
            MostLuckers           = maxLuckers  > 0 ? MaxRecord(maxLuckers,  e => e.Luckers)     : [],
            MostOnlyOnes          = maxOnlyOnes > 0 ? MaxRecord(maxOnlyOnes, e => e.OnlyOnes)    : [],
            LongestPositiveStreak = posStreakRecords,
            LongestNegativeStreak = negStreakRecords
        };
    }

    public async Task<List<UserChampionshipStatsDto>> GetUserChampionshipStatsAsync(Guid userId, ChampionshipType? type = null)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var championships = await context.Championships
            .Where(c => !c.IsTest)
            .Where(c => type == null || c.Type == type)
            .OrderByDescending(c => c.Year)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.Name, c.Year })
            .ToListAsync();

        if (championships.Count == 0)
            return [];

        var championshipIds = championships.Select(c => c.Id).ToList();

        // Single projected scan over every prediction in these championships, carrying
        // everything needed for this user's stats, ranks, OnlyOneTries, and the profile below.
        var allPredictions = await context.Predictions
            .Where(p => championshipIds.Contains(p.Match.ChampionshipId))
            .Select(p => new
            {
                p.Match.ChampionshipId,
                p.MatchId,
                p.UserId,
                p.Points,
                p.GotWinner,
                p.OneGoalMiss,
                p.GotExactScore,
                p.IsOnlyCorrect,
                p.RarityPart,
                p.PredictedHome,
                p.PredictedAway,
                IsScored = p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue,
                HomeTeam = p.Match.HomeTeam,
                AwayTeam = p.Match.AwayTeam
            })
            .ToListAsync();

        var allWinnerPoints = await context.ChampionshipWinnerPredictions
            .Where(p => championshipIds.Contains(p.ChampionshipId) && p.PointsAwarded.HasValue)
            .Select(p => new { p.ChampionshipId, p.UserId, Points = p.PointsAwarded!.Value })
            .ToListAsync();

        var allWinnerLookup = allWinnerPoints
            .ToDictionary(p => (p.ChampionshipId, p.UserId), p => p.Points);

        var winnerPoints = allWinnerPoints
            .Where(p => p.UserId == userId)
            .ToDictionary(p => p.ChampionshipId, p => p.Points);

        var rawLookup = allPredictions
            .Where(p => p.UserId == userId)
            .GroupBy(p => p.ChampionshipId)
            .ToDictionary(g => g.Key, g => new
            {
                TotalPoints = g.Sum(p => p.Points),
                CorrectWinners = g.Count(p => p.GotWinner),
                OneGoalMisses = g.Count(p => p.OneGoalMiss),
                ExactScores = g.Count(p => p.GotExactScore),
                OnlyCorrect = g.Count(p => p.IsOnlyCorrect)
            });

        // OnlyOneTries per championship for this user (scored matches only)
        var onlyOneTries = allPredictions
            .Where(p => p.IsScored)
            .GroupBy(p => new { p.ChampionshipId, p.MatchId, Winner = p.PredictedHome > p.PredictedAway ? "H" : p.PredictedHome < p.PredictedAway ? "A" : "D" })
            .Where(g => g.Count() == 1)
            .SelectMany(g => g)
            .Where(p => p.UserId == userId)
            .GroupBy(p => p.ChampionshipId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Rank computation — all users' stats per championship
        var allPredStats = allPredictions
            .GroupBy(p => new { p.ChampionshipId, p.UserId })
            .Select(g => new
            {
                ChampionshipId = g.Key.ChampionshipId,
                UserId         = g.Key.UserId,
                Points         = g.Sum(p => p.Points),
                CorrectWinners = g.Count(p => p.GotWinner),
                OneGoalMisses  = g.Count(p => p.OneGoalMiss),
                OnlyCorrect    = g.Count(p => p.IsOnlyCorrect),
                RarityPoints   = g.Sum(p => p.RarityPart)
            })
            .ToList();

        var ranks = new Dictionary<Guid, (int Rank, int TotalParticipants)>();
        foreach (var champId in championshipIds)
        {
            var participants = allPredStats
                .Where(p => p.ChampionshipId == champId)
                .Select(p => new
                {
                    p.UserId,
                    Total          = p.Points + allWinnerLookup.GetValueOrDefault((champId, p.UserId), 0),
                    p.CorrectWinners,
                    p.OneGoalMisses,
                    p.OnlyCorrect,
                    p.RarityPoints
                })
                .OrderByDescending(p => p.Total)
                .ThenByDescending(p => p.CorrectWinners)
                .ThenByDescending(p => p.OneGoalMisses)
                .ThenByDescending(p => p.OnlyCorrect)
                .ThenByDescending(p => p.RarityPoints)
                .ToList();

            var index = participants.FindIndex(p => p.UserId == userId);
            ranks[champId] = (index >= 0 ? index + 1 : 0, participants.Count);
        }

        // Per-championship prediction profile for this user
        var profilePreds = allPredictions
            .Where(p => p.UserId == userId && p.IsScored)
            .ToList();

        var profileByChampionship = profilePreds
            .GroupBy(p => p.ChampionshipId)
            .ToDictionary(g => g.Key, g =>
            {
                var preds = g.ToList();

                var topPred = preds
                    .GroupBy(p => p.PredictedHome >= p.PredictedAway
                        ? (p.PredictedHome, p.PredictedAway)
                        : (p.PredictedAway, p.PredictedHome))
                    .Select(sg => (Score: $"{sg.Key.Item1}:{sg.Key.Item2}", Count: sg.Count()))
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefault();

                var expanded = preds.SelectMany(p => new[]
                {
                    new { Team = p.HomeTeam, p.GotWinner },
                    new { Team = p.AwayTeam, p.GotWinner }
                }).ToList();

                var best = expanded.Where(x => x.GotWinner)
                    .GroupBy(x => x.Team)
                    .Select(sg => (Team: sg.Key, Count: sg.Count()))
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefault();

                var worst = expanded.Where(x => !x.GotWinner)
                    .GroupBy(x => x.Team)
                    .Select(sg => (Team: sg.Key, Count: sg.Count()))
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefault();

                return (TopPred: topPred.Score, TopPredCount: topPred.Count, BestTeam: best.Team, BestTeamCount: best.Count, WorstTeam: worst.Team, WorstTeamCount: worst.Count);
            });

        return championships
            .Where(c => rawLookup.ContainsKey(c.Id))
            .Select(c =>
            {
                profileByChampionship.TryGetValue(c.Id, out var prof);
                return new UserChampionshipStatsDto
                {
                    ChampionshipId           = c.Id,
                    ChampionshipName         = c.Name,
                    Year                     = c.Year,
                    TotalPoints              = rawLookup[c.Id].TotalPoints + winnerPoints.GetValueOrDefault(c.Id, 0),
                    CorrectWinners           = rawLookup[c.Id].CorrectWinners,
                    OneGoalMisses            = rawLookup[c.Id].OneGoalMisses,
                    ExactScores              = rawLookup[c.Id].ExactScores,
                    OnlyCorrect              = rawLookup[c.Id].OnlyCorrect,
                    OnlyOneTries             = onlyOneTries.GetValueOrDefault(c.Id, 0),
                    Rank                     = ranks.TryGetValue(c.Id, out var r) ? r.Rank : 0,
                    TotalParticipants        = ranks.TryGetValue(c.Id, out var t) ? t.TotalParticipants : 0,
                    MostCommonPrediction      = prof.TopPred,
                    MostCommonPredictionCount = prof.TopPredCount,
                    BestTeam                  = prof.BestTeam,
                    BestTeamCount             = prof.BestTeamCount,
                    WorstTeam                 = prof.WorstTeam,
                    WorstTeamCount            = prof.WorstTeamCount
                };
            })
            .ToList();
    }

    public async Task<LineGraphViewModel> GetLeaderboardProgressAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var matches = await context.Matches
            .Where(m => m.ChampionshipId == championshipId && m.HomeScore.HasValue && m.AwayScore.HasValue)
            .OrderBy(m => m.StartTimeUtc)
            .ToListAsync();

        var matchIds = matches.Select(m => m.Id).ToList();
        var predictions = await context.Predictions
            .Where(p => matchIds.Contains(p.MatchId))
            .ToListAsync();

        var userIdsWithPredictions = predictions.Select(p => p.UserId).Distinct().ToList();

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var users = await userManager.Users
            .Where(u => userIdsWithPredictions.Contains(u.Id))
            .ToListAsync();

        var userNames = users.ToDictionary(u => u.Id, u => u.UserName ?? "Unknown");
        return BuildLeaderboardProgress(matches, predictions, userNames);
    }

    public LineGraphViewModel BuildLeaderboardProgress(
        IList<Match> scoredMatches,
        IList<Prediction> predictions,
        IReadOnlyDictionary<Guid, string> userNames)
    {
        var predictionLookup = predictions.ToDictionary(p => (p.MatchId, p.UserId));

        var graph = new LineGraphViewModel { Labels = ["Start"] };
        graph.Labels.AddRange(scoredMatches.Select(m => $"{m.HomeTeam}-{m.AwayTeam}"));

        foreach (var (userId, userName) in userNames)
        {
            var cumulative = 0;
            var data = new List<int> { 0 };

            foreach (var match in scoredMatches)
            {
                if (predictionLookup.TryGetValue((match.Id, userId), out var prediction))
                    cumulative += prediction.Points;

                data.Add(cumulative);
            }

            graph.Series.Add(new LineSeriesDto { Name = userName, Data = data });
        }

        return graph;
    }

    public async Task<GlobalStatsDto> GetGlobalStatsAsync(ChampionshipType? type = null)
    {
        return await _cache.GetOrCreateAsync(GlobalStatsCacheKey(type), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = AggregateCacheDuration;
            return await BuildGlobalStatsAsync(type);
        }) ?? new GlobalStatsDto();
    }

    private async Task<GlobalStatsDto> BuildGlobalStatsAsync(ChampionshipType? type)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var predictions = await context.Predictions
            .Where(p => p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue)
            .Where(p => type == null || p.Match.Championship.Type == type)
            .Select(p => new
            {
                p.UserId,
                p.MatchId,
                p.PredictedHome,
                p.PredictedAway,
                p.GotExactScore,
                ActualHome = p.Match.HomeScore!.Value,
                ActualAway = p.Match.AwayScore!.Value
            })
            .ToListAsync();

        if (predictions.Count == 0)
            return new GlobalStatsDto();

        static (int, int) Normalize(int a, int b) => a >= b ? (a, b) : (b, a);
        static string FormatNorm((int h, int a) n) => $"{n.h}:{n.a}";

        // Top 3 results — one result per unique match
        var matchResults = predictions.DistinctBy(p => p.MatchId).ToList();
        int totalMatches = matchResults.Count;
        var topResults = matchResults
            .GroupBy(p => Normalize(p.ActualHome, p.ActualAway))
            .Select(g => (Score: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Select(x => (FormatNorm(x.Score), x.Count))
            .ToList();

        // Top 5 luckers — exact-score correct predictions, normalized
        var exactPredictions = predictions.Where(p => p.GotExactScore).ToList();
        int totalExact = exactPredictions.Count;
        var topLuckers = exactPredictions
            .GroupBy(p => Normalize(p.PredictedHome, p.PredictedAway))
            .Select(g => (Score: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Select(x => (FormatNorm(x.Score), x.Count))
            .ToList();

        // All prediction groups with per-group user info
        var predGroups = predictions
            .GroupBy(p => Normalize(p.PredictedHome, p.PredictedAway))
            .Select(g =>
            {
                var userGroups = g.GroupBy(p => p.UserId)
                    .Select(ug => (UserId: ug.Key, Count: ug.Count()))
                    .OrderByDescending(x => x.Count)
                    .ToList();
                var topUser = userGroups.FirstOrDefault();
                var allUserIds = userGroups.Select(u => u.UserId).ToList();
                return new { Score = g.Key, Count = g.Count(), TopUserId = topUser.UserId, TopUserCount = topUser.Count, AllUserIds = allUserIds };
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var leastGroups = predGroups.OrderBy(x => x.Count).Take(3).ToList();

        // Collect all user IDs we need to resolve
        var userIdsNeeded = new HashSet<Guid>();
        foreach (var pg in predGroups.Take(3))
            userIdsNeeded.Add(pg.TopUserId);
        foreach (var pg in leastGroups)
            foreach (var uid in pg.AllUserIds)
                userIdsNeeded.Add(uid);

        var userNames = new Dictionary<Guid, string>();
        if (userIdsNeeded.Count > 0)
        {
            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            userNames = await userManager.Users
                .Where(u => userIdsNeeded.Contains(u.Id) && u.UserName != null)
                .ToDictionaryAsync(u => u.Id, u => u.UserName!);
        }

        var topPredictions = predGroups.Take(3)
            .Select(pg => (FormatNorm(pg.Score), pg.Count,
                userNames.GetValueOrDefault(pg.TopUserId),
                pg.TopUserCount))
            .ToList();

        var leastPredictions = leastGroups
            .Select(pg => (FormatNorm(pg.Score), pg.Count,
                pg.AllUserIds.Select(uid => userNames.GetValueOrDefault(uid)).Where(n => n != null).Select(n => n!).ToList()))
            .ToList();

        return new GlobalStatsDto
        {
            TopResults = topResults,
            TotalMatchesWithResults = totalMatches,
            TopLuckers = topLuckers,
            TotalExactScorePredictions = totalExact,
            TopPredictions = topPredictions,
            LeastCommonPredictions = leastPredictions
        };
    }

    public async Task<Dictionary<Guid, UserPredictionProfileDto>> GetUserPredictionProfilesAsync(ChampionshipType? type = null)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var predictions = await context.Predictions
            .Where(p => p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue)
            .Where(p => !p.Match.Championship.IsTest)
            .Where(p => type == null || p.Match.Championship.Type == type)
            .Select(p => new
            {
                p.UserId,
                p.PredictedHome,
                p.PredictedAway,
                p.GotWinner,
                HomeTeam = p.Match.HomeTeam,
                AwayTeam = p.Match.AwayTeam
            })
            .ToListAsync();

        if (predictions.Count == 0)
            return new Dictionary<Guid, UserPredictionProfileDto>();

        var userIds = predictions.Select(p => p.UserId).Distinct().ToList();

        // Most common prediction per user (normalized: higher score first)
        var mostCommon = predictions
            .GroupBy(p => p.UserId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(p => p.PredictedHome >= p.PredictedAway
                        ? (p.PredictedHome, p.PredictedAway)
                        : (p.PredictedAway, p.PredictedHome))
                    .Select(sg => (Score: $"{sg.Key.Item1}:{sg.Key.Item2}", Count: sg.Count()))
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefault()
            );

        // Expand each prediction to both participating teams
        var expanded = predictions
            .SelectMany(p => new[]
            {
                new { p.UserId, Team = p.HomeTeam, p.GotWinner },
                new { p.UserId, Team = p.AwayTeam, p.GotWinner }
            })
            .ToList();

        // Best team: team whose matches the user most often correctly predicted
        var bestTeam = expanded
            .Where(x => x.GotWinner)
            .GroupBy(x => (x.UserId, x.Team))
            .Select(g => new { g.Key.UserId, g.Key.Team, Count = g.Count() })
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Count).First()
            );

        // Worst team: team whose matches the user most often incorrectly predicted
        var worstTeam = expanded
            .Where(x => !x.GotWinner)
            .GroupBy(x => (x.UserId, x.Team))
            .Select(g => new { g.Key.UserId, g.Key.Team, Count = g.Count() })
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Count).First()
            );

        return userIds.ToDictionary(
            id => id,
            id => new UserPredictionProfileDto
            {
                MostCommonPrediction = mostCommon.TryGetValue(id, out var pred) ? pred.Score : null,
                MostCommonPredictionCount = mostCommon.TryGetValue(id, out var pred2) ? pred2.Count : 0,
                BestTeam = bestTeam.TryGetValue(id, out var best) ? best.Team : null,
                BestTeamCount = bestTeam.TryGetValue(id, out var best2) ? best2.Count : 0,
                WorstTeam = worstTeam.TryGetValue(id, out var worst) ? worst.Team : null,
                WorstTeamCount = worstTeam.TryGetValue(id, out var worst2) ? worst2.Count : 0
            }
        );
    }

    public async Task<Dictionary<Guid, (int TotalWinners, int TotalLuckers, int TotalOnlyOnes)>> GetChampionshipAggregatesAsync(IEnumerable<Guid> championshipIds)
    {
        var ids = championshipIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, (int, int, int)>();

        // Cached per championship (rather than per requested set) so a result in one
        // championship only invalidates that championship's entry.
        var result = new Dictionary<Guid, (int TotalWinners, int TotalLuckers, int TotalOnlyOnes)>();
        var missingIds = new List<Guid>();
        foreach (var id in ids)
        {
            if (_cache.TryGetValue(AggregatesCacheKey(id), out (int TotalWinners, int TotalLuckers, int TotalOnlyOnes) cached))
                result[id] = cached;
            else
                missingIds.Add(id);
        }

        if (missingIds.Count > 0)
        {
            using var context = _dbContextFactory.CreateDbContext();

            var rows = await context.Predictions
                .Where(p => missingIds.Contains(p.Match.ChampionshipId))
                .GroupBy(p => p.Match.ChampionshipId)
                .Select(g => new
                {
                    ChampionshipId = g.Key,
                    TotalWinners   = g.Count(p => p.GotWinner),
                    TotalLuckers   = g.Count(p => p.GotExactScore),
                    TotalOnlyOnes  = g.Count(p => p.IsOnlyCorrect)
                })
                .ToListAsync();

            var byId = rows.ToDictionary(r => r.ChampionshipId, r => (r.TotalWinners, r.TotalLuckers, r.TotalOnlyOnes));

            foreach (var id in missingIds)
            {
                var value = byId.GetValueOrDefault(id, (0, 0, 0));
                result[id] = value;
                _cache.Set(AggregatesCacheKey(id), value, AggregateCacheDuration);
            }
        }

        return result;
    }

    public async Task<UserPredictionProfileDto?> GetUserPredictionProfileAsync(Guid userId, ChampionshipType? type = null)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var predictions = await context.Predictions
            .Where(p => p.UserId == userId)
            .Where(p => p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue)
            .Where(p => !p.Match.Championship.IsTest)
            .Where(p => type == null || p.Match.Championship.Type == type)
            .Select(p => new
            {
                p.PredictedHome,
                p.PredictedAway,
                p.GotWinner,
                HomeTeam = p.Match.HomeTeam,
                AwayTeam = p.Match.AwayTeam
            })
            .ToListAsync();

        if (predictions.Count == 0)
            return null;

        var topPred = predictions
            .GroupBy(p => p.PredictedHome >= p.PredictedAway
                ? (p.PredictedHome, p.PredictedAway)
                : (p.PredictedAway, p.PredictedHome))
            .Select(g => (Score: $"{g.Key.Item1}:{g.Key.Item2}", Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        var expanded = predictions.SelectMany(p => new[]
        {
            new { Team = p.HomeTeam, p.GotWinner },
            new { Team = p.AwayTeam, p.GotWinner }
        }).ToList();

        var best = expanded.Where(x => x.GotWinner)
            .GroupBy(x => x.Team)
            .Select(g => (Team: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        var worst = expanded.Where(x => !x.GotWinner)
            .GroupBy(x => x.Team)
            .Select(g => (Team: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        return new UserPredictionProfileDto
        {
            MostCommonPrediction = topPred.Score,
            MostCommonPredictionCount = topPred.Count,
            BestTeam = best.Team,
            BestTeamCount = best.Count,
            WorstTeam = worst.Team,
            WorstTeamCount = worst.Count
        };
    }

    public async Task<(Guid? WinnerUserId, Guid? LoserUserId, string ChampionshipLabel)?> GetPreviousChampionshipExtremesAsync(Guid currentChampionshipId, ChampionshipType type)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var currentYear = await context.Championships
            .Where(c => c.Id == currentChampionshipId)
            .Select(c => c.Year)
            .FirstOrDefaultAsync();

        var previous = await context.Championships
            .Where(c => c.Year <= currentYear && c.Id != currentChampionshipId && c.Type == type && c.IsChampionshipEnded && !c.IsTest)
            .OrderByDescending(c => c.Year)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.Name, c.Year })
            .FirstOrDefaultAsync();

        if (previous == null)
            return null;

        var scores = await context.Predictions
            .Where(p => p.Match.ChampionshipId == previous.Id)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(p => p.Points) })
            .ToListAsync();

        var winnerPredPoints = await context.ChampionshipWinnerPredictions
            .Where(p => p.ChampionshipId == previous.Id && p.PointsAwarded.HasValue)
            .Select(p => new { p.UserId, Points = p.PointsAwarded!.Value })
            .ToListAsync();

        var winnerPredLookup = winnerPredPoints.ToDictionary(p => p.UserId, p => p.Points);

        var ranked = scores
            .Select(s => new { s.UserId, Total = s.Points + winnerPredLookup.GetValueOrDefault(s.UserId, 0) })
            .OrderByDescending(s => s.Total)
            .ToList();

        if (ranked.Count == 0)
            return null;

        var winnerId = ranked[0].UserId;
        var loserId = ranked[^1].UserId;

        return (winnerId, ranked.Count > 1 ? loserId : (Guid?)null, $"{previous.Name} {previous.Year}");
    }
}
