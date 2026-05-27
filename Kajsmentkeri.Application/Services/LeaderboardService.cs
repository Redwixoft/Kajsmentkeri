using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Application.Models;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kajsmentkeri.Application.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IServiceScopeFactory _scopeFactory;

    public LeaderboardService(IServiceScopeFactory scopeFactory, IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _scopeFactory = scopeFactory;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var grouped = await context.Predictions
            .Where(p => p.Match.ChampionshipId == championshipId)
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
            .ToListAsync();

        var winnerPoints = await context.ChampionshipWinnerPredictions
            .Where(p => p.ChampionshipId == championshipId && p.PointsAwarded.HasValue)
            .ToDictionaryAsync(p => p.UserId, p => p.PointsAwarded!.Value);

        // Compute OnlyOneTries: scored matches where the user was the sole predictor of their chosen outcome
        var scoredPredictions = await context.Predictions
            .Where(p => p.Match.ChampionshipId == championshipId && p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue)
            .Select(p => new { p.MatchId, p.UserId, p.PredictedHome, p.PredictedAway })
            .ToListAsync();

        var onlyOneTries = scoredPredictions
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
        using var context = _dbContextFactory.CreateDbContext();
        var grouped = await context.Predictions
            .Where(p => !p.Match.Championship.IsTest)
            .Where(p => type == null || p.Match.Championship.Type == type)
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
                TotalScoredPredictions = g.Count(p => p.Match.HomeScore.HasValue)
            })
            .ToListAsync();

        var winnerPointsGlobal = await context.ChampionshipWinnerPredictions
            .Where(p => !p.Championship.IsTest && p.PointsAwarded.HasValue)
            .Where(p => type == null || p.Championship.Type == type)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(p => p.PointsAwarded!.Value) })
            .ToDictionaryAsync(p => p.UserId, p => p.Points);

        var championshipCounts = await context.Predictions
            .Where(p => !p.Match.Championship.IsTest)
            .Where(p => type == null || p.Match.Championship.Type == type)
            .Select(p => new { p.UserId, p.Match.ChampionshipId })
            .Distinct()
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        // Compute OnlyOneTries: scored matches where the user was the sole predictor of their chosen outcome
        var allScoredPredictions = await context.Predictions
            .Where(p => !p.Match.Championship.IsTest && p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue)
            .Where(p => type == null || p.Match.Championship.Type == type)
            .Select(p => new { p.MatchId, p.UserId, p.PredictedHome, p.PredictedAway })
            .ToListAsync();

        var onlyOneTries = allScoredPredictions
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
                ChampionshipCount = championshipCounts.GetValueOrDefault(g.UserId, 0),
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

            for (int i = 0; i < Math.Min(3, ranked.Count); i++)
            {
                var userId = ranked[i].UserId;
                if (!medals.TryGetValue(userId, out var list))
                    medals[userId] = list = new List<(int, string, int)>();
                list.Add((i + 1, championship.Name, championship.Year));
            }

            // Last place (only when there are more than 3 participants)
            if (ranked.Count > 3)
            {
                var lastUserId = ranked[^1].UserId;
                if (!medals.TryGetValue(lastUserId, out var lastList))
                    medals[lastUserId] = lastList = new List<(int, string, int)>();
                lastList.Add((ranked.Count, championship.Name, championship.Year));
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

        var standingsByChamp = new Dictionary<Guid, List<Guid>>();
        foreach (var champId in ids)
        {
            var ranked = scores
                .Where(s => s.ChampionshipId == champId)
                .Select(s => new { s.UserId, Total = s.Points + winnerPredLookup.GetValueOrDefault((champId, s.UserId), 0) })
                .OrderByDescending(s => s.Total)
                .Select(s => s.UserId)
                .ToList();
            standingsByChamp[champId] = ranked;
        }

        var allRelevantUserIds = standingsByChamp.Values
            .SelectMany(list => list.Take(topN))
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
            var ranked = standingsByChamp.TryGetValue(champId, out var r) ? r : new List<Guid>();
            var entries = new List<(string UserName, int Position)>();

            for (int i = 0; i < Math.Min(topN, ranked.Count); i++)
            {
                if (userNames.TryGetValue(ranked[i], out var name))
                    entries.Add((name, i + 1));
            }

            if (currentUserId.HasValue)
            {
                var currentPos = ranked.IndexOf(currentUserId.Value);
                if (currentPos >= topN && userNames.TryGetValue(currentUserId.Value, out var currentName))
                    entries.Add((currentName, currentPos + 1));
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

        var rawStats = await context.Predictions
            .Where(p => championshipIds.Contains(p.Match.ChampionshipId) && p.UserId == userId)
            .GroupBy(p => p.Match.ChampionshipId)
            .Select(g => new
            {
                ChampionshipId = g.Key,
                Points = g.Sum(p => p.Points),
                Winners = g.Count(p => p.GotWinner),
                Misses = g.Count(p => p.OneGoalMiss),
                Luckers = g.Count(p => p.GotExactScore),
                OnlyOnes = g.Count(p => p.IsOnlyCorrect)
            })
            .ToListAsync();

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

        var scoredPredictions = await context.Predictions
            .Where(p => championshipIds.Contains(p.Match.ChampionshipId)
                     && p.UserId == userId
                     && p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue)
            .Select(p => new { p.Match.ChampionshipId, p.GotWinner, p.Match.StartTimeUtc })
            .ToListAsync();

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

        var rawStats = await context.Predictions
            .Where(p => championshipIds.Contains(p.Match.ChampionshipId) && p.UserId == userId)
            .GroupBy(p => p.Match.ChampionshipId)
            .Select(g => new
            {
                ChampionshipId = g.Key,
                TotalPoints = g.Sum(p => p.Points),
                CorrectWinners = g.Count(p => p.GotWinner),
                OneGoalMisses = g.Count(p => p.OneGoalMiss),
                ExactScores = g.Count(p => p.GotExactScore),
                OnlyCorrect = g.Count(p => p.IsOnlyCorrect)
            })
            .ToListAsync();

        var winnerPoints = await context.ChampionshipWinnerPredictions
            .Where(p => championshipIds.Contains(p.ChampionshipId) && p.UserId == userId && p.PointsAwarded.HasValue)
            .Select(p => new { p.ChampionshipId, Points = p.PointsAwarded!.Value })
            .ToDictionaryAsync(p => p.ChampionshipId, p => p.Points);

        // OnlyOneTries per championship for this user
        var allScoredPreds = await context.Predictions
            .Where(p => championshipIds.Contains(p.Match.ChampionshipId)
                     && p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue)
            .Select(p => new { p.Match.ChampionshipId, p.MatchId, p.UserId, p.PredictedHome, p.PredictedAway })
            .ToListAsync();

        var onlyOneTries = allScoredPreds
            .GroupBy(p => new { p.ChampionshipId, p.MatchId, Winner = p.PredictedHome > p.PredictedAway ? "H" : p.PredictedHome < p.PredictedAway ? "A" : "D" })
            .Where(g => g.Count() == 1)
            .SelectMany(g => g)
            .Where(p => p.UserId == userId)
            .GroupBy(p => p.ChampionshipId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Rank computation — load all users' stats per championship
        var allPredStats = await context.Predictions
            .Where(p => championshipIds.Contains(p.Match.ChampionshipId))
            .GroupBy(p => new { p.Match.ChampionshipId, p.UserId })
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
            .ToListAsync();

        var allWinnerPoints = await context.ChampionshipWinnerPredictions
            .Where(p => championshipIds.Contains(p.ChampionshipId) && p.PointsAwarded.HasValue)
            .Select(p => new { p.ChampionshipId, p.UserId, Points = p.PointsAwarded!.Value })
            .ToListAsync();

        var allWinnerLookup = allWinnerPoints
            .ToDictionary(p => (p.ChampionshipId, p.UserId), p => p.Points);

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

        var rawLookup = rawStats.ToDictionary(s => s.ChampionshipId);

        // Per-championship prediction profile for this user
        var profilePreds = await context.Predictions
            .Where(p => p.UserId == userId && championshipIds.Contains(p.Match.ChampionshipId))
            .Where(p => p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue)
            .Select(p => new
            {
                p.Match.ChampionshipId,
                p.PredictedHome,
                p.PredictedAway,
                p.GotWinner,
                HomeTeam = p.Match.HomeTeam,
                AwayTeam = p.Match.AwayTeam
            })
            .ToListAsync();

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
            foreach (var uid in userIdsNeeded)
            {
                var u = await userManager.FindByIdAsync(uid.ToString());
                if (u?.UserName != null) userNames[uid] = u.UserName;
            }
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

        using var context = _dbContextFactory.CreateDbContext();

        var rows = await context.Predictions
            .Where(p => ids.Contains(p.Match.ChampionshipId))
            .GroupBy(p => p.Match.ChampionshipId)
            .Select(g => new
            {
                ChampionshipId = g.Key,
                TotalWinners   = g.Count(p => p.GotWinner),
                TotalLuckers   = g.Count(p => p.GotExactScore),
                TotalOnlyOnes  = g.Count(p => p.IsOnlyCorrect)
            })
            .ToListAsync();

        return rows.ToDictionary(
            r => r.ChampionshipId,
            r => (r.TotalWinners, r.TotalLuckers, r.TotalOnlyOnes));
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
}
