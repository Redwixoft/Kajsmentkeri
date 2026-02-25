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
            .ToList();

        return leaderboard;
    }

    public async Task<List<LeaderboardEntryDto>> GetGlobalLeaderboardAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();
        var grouped = await context.Predictions
            .Where(p => !p.Match.Championship.IsTest)
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

        var winnerPointsGlobal = await context.ChampionshipWinnerPredictions
            .Where(p => !p.Championship.IsTest && p.PointsAwarded.HasValue)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(p => p.PointsAwarded!.Value) })
            .ToDictionaryAsync(p => p.UserId, p => p.Points);

        var championshipCounts = await context.Predictions
            .Where(p => !p.Match.Championship.IsTest)
            .Select(p => new { p.UserId, p.Match.ChampionshipId })
            .Distinct()
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        // Compute OnlyOneTries: scored matches where the user was the sole predictor of their chosen outcome
        var allScoredPredictions = await context.Predictions
            .Where(p => !p.Match.Championship.IsTest && p.Match.HomeScore.HasValue && p.Match.AwayScore.HasValue)
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
                ChampionshipCount = championshipCounts.GetValueOrDefault(g.UserId, 0)
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.CorrectWinners)
            .ThenByDescending(x => x.OneGoalMisses)
            .ThenByDescending(x => x.OnlyCorrect)
            .ToList();

        return leaderboard;
    }

    public async Task<Dictionary<Guid, List<(int Position, string ChampionshipName, int Year)>>> GetMedalCountsAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();

        var qualifyingChampionships = await context.Championships
            .Where(c => !c.IsTest && (c.IsChampionshipEnded || c.Year < 2026))
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
                OnlyCorrect = g.Count(p => p.IsOnlyCorrect)
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
                    p.OnlyCorrect
                })
                .OrderByDescending(e => e.Total)
                .ThenByDescending(e => e.CorrectWinners)
                .ThenByDescending(e => e.OneGoalMisses)
                .ThenByDescending(e => e.OnlyCorrect)
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

    public async Task<LineGraphViewModel> GetLeaderboardProgressAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var matches = await context.Matches
            .Where(m => m.ChampionshipId == championshipId && m.HomeScore.HasValue && m.AwayScore.HasValue)
            .OrderBy(m => m.StartTimeUtc)
            .ToListAsync();

        var predictions = await context.Predictions
            .Where(p => matches.Select(m => m.Id).Contains(p.MatchId))
            .ToListAsync();

        var userIdsWithPredictions = predictions.Select(p => p.UserId).Distinct().ToList();

        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var users = await userManager.Users
            .Where(u => userIdsWithPredictions.Contains(u.Id))
            .ToListAsync();

        var graph = new LineGraphViewModel
        {
            Labels = ["Start"]
        };
        graph.Labels.AddRange(matches.Select(m => $"{m.HomeTeam}-{m.AwayTeam}").ToList());

        foreach (var user in users)
        {
            var cumulative = 0;
            var data = new List<int>() { 0 };

            foreach (var match in matches)
            {
                var prediction = predictions.FirstOrDefault(p => p.MatchId == match.Id && p.UserId == user.Id);
                if (prediction != null)
                    cumulative += prediction.Points;

                data.Add(cumulative);
            }

            graph.Series.Add(new LineSeriesDto
            {
                Name = user.UserName ?? "Unknown",
                Data = data
            });
        }

        return graph;
    }
}
