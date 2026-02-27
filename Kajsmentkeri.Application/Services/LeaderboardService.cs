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
}
