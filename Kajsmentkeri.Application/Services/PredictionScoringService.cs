using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class PredictionScoringService : IPredictionScoringService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public PredictionScoringService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task RecalculateForMatchAsync(Guid matchId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var match = await context.Matches
            .Include(m => m.Championship)
                .ThenInclude(c => c.ScoringRules)
            .Include(m => m.Predictions)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null || match.HomeScore == null || match.AwayScore == null)
            return; // No result entered yet

        var predictions = match.Predictions.ToList();
        var rules = match.Championship.ScoringRules;

        var correctWinnerIds = predictions
            .Where(p => GetWinner(p.PredictedHome, p.PredictedAway) == GetWinner(match.HomeScore.Value, match.AwayScore.Value))
            .Select(p => p.UserId)
            .ToHashSet();

        foreach (var prediction in predictions)
        {
            int points = 0;

            var gotWinner = GetWinner(prediction.PredictedHome, prediction.PredictedAway) ==
                            GetWinner(match.HomeScore.Value, match.AwayScore.Value);

            var gotExact = prediction.PredictedHome == match.HomeScore &&
                           prediction.PredictedAway == match.AwayScore;

            var oneGoalMiss = IsOneGoalOff(prediction, match);

            var isOnlyCorrect = gotWinner && correctWinnerIds.Count == 1;

            var rarityPart = 0m;
            if (correctWinnerIds.Count > 0)
            {
                rarityPart = Math.Round((decimal)rules.RarityPointsBonus / correctWinnerIds.Count, 2) * (gotWinner ? 1 : 0);
            }

            if (gotWinner) points += rules.PointsForCorrectWinner;
            if (gotExact) points += rules.PointsForExactScore;
            if (isOnlyCorrect) points += rules.PointsForOnlyCorrectWinner;

            prediction.GotWinner = gotWinner;
            prediction.GotExactScore = gotExact;
            prediction.OneGoalMiss = oneGoalMiss;
            prediction.IsOnlyCorrect = isOnlyCorrect;
            prediction.Points = points;
            prediction.RarityPart = rarityPart;
        }

        await context.SaveChangesAsync();
    }

    private static string GetWinner(int home, int away)
    {
        return home > away ? "H" : home < away ? "A" : "D";
    }

    private bool IsOneGoalOff(Prediction p, Match m)
    {
        var homeDiff = Math.Abs(p.PredictedHome - m.HomeScore!.Value);
        var awayDiff = Math.Abs(p.PredictedAway - m.AwayScore!.Value);

        return (homeDiff == 1 && awayDiff == 0) || (homeDiff == 0 && awayDiff == 1);
    }
}
