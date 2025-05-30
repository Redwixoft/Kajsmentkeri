using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Application.Models;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public LeaderboardService(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(Guid championshipId)
    {
        var grouped = await _db.Predictions
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

        // Load all relevant users
        var userIds = grouped.Select(g => g.UserId).ToList();
        var users = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName);

        // Merge results
        var leaderboard = grouped.Select(g => new LeaderboardEntryDto
            {
                UserId = g.UserId,
                UserName = users.TryGetValue(g.UserId, out var name) ? name ?? "???" : "Unknown",
                TotalPoints = g.TotalPoints,
                CorrectWinners = g.CorrectWinners,
                OneGoalMisses = g.OneGoalMisses,
                OnlyCorrect = g.OnlyCorrect,
                ExactScores = g.ExactScores,
                SpecialLeaderboardPoints = g.RarityPoints
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.CorrectWinners)
            .ThenByDescending(x => x.OneGoalMisses)
            .ThenByDescending(x => x.OnlyCorrect)
            .ToList();

        return leaderboard;
    }

    public async Task<LineGraphViewModel> GetLeaderboardProgressAsync(Guid championshipId)
    {
        var matches = await _db.Matches
            .Where(m => m.ChampionshipId == championshipId && m.HomeScore.HasValue && m.AwayScore.HasValue)
            .OrderBy(m => m.StartTimeUtc)
            .ToListAsync();

        var predictions = await _db.Predictions
            .Where(p => matches.Select(m => m.Id).Contains(p.MatchId))
            .ToListAsync();

        var users = await _userManager.Users.ToListAsync();

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
