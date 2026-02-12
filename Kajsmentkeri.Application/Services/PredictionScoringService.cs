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
            return;

        CalculateMatchPredictions(match);
        await context.SaveChangesAsync();
    }

    public async Task RecalculateForChampionshipAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var matches = await context.Matches
            .Where(m => m.ChampionshipId == championshipId)
            .Include(m => m.Championship)
                .ThenInclude(c => c.ScoringRules)
            .Include(m => m.Predictions)
            .ToListAsync();

        foreach (var match in matches)
        {
            if (match.HomeScore != null && match.AwayScore != null)
            {
                CalculateMatchPredictions(match);
            }
        }

        // Also update championship winner points if they are already awarded
        var ch = await context.Championships.Include(c => c.ScoringRules).FirstOrDefaultAsync(c => c.Id == championshipId);
        if (ch != null && ch.IsChampionshipEnded)
        {
            var endMatches = await context.Matches
                .Where(m => m.ChampionshipId == championshipId && m.HomeScore.HasValue && m.AwayScore.HasValue)
                .OrderByDescending(m => m.StartTimeUtc)
                .Take(2)
                .ToListAsync();

            if (endMatches.Count >= 2)
            {
                var finalMatch = endMatches[0];
                var bronzeMatch = endMatches[1];

                string goldTeam = finalMatch.HomeScore > finalMatch.AwayScore ? finalMatch.HomeTeam : finalMatch.AwayTeam;
                string silverTeam = finalMatch.HomeScore > finalMatch.AwayScore ? finalMatch.AwayTeam : finalMatch.HomeTeam;
                string bronzeTeam = bronzeMatch.HomeScore > bronzeMatch.AwayScore ? bronzeMatch.HomeTeam : bronzeMatch.AwayTeam;

                var winnerPredictions = await context.ChampionshipWinnerPredictions.Where(p => p.ChampionshipId == championshipId).ToListAsync();
                foreach (var pred in winnerPredictions)
                {
                    if (pred.TeamName == goldTeam)
                        pred.PointsAwarded = ch.ScoringRules.PointsForChampionshipWinner;
                    else if (pred.TeamName == silverTeam)
                        pred.PointsAwarded = ch.ScoringRules.PointsForChampionshipRunnerUp;
                    else if (pred.TeamName == bronzeTeam)
                        pred.PointsAwarded = ch.ScoringRules.PointsForChampionshipThirdPlace;
                    else
                        pred.PointsAwarded = 0;
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private void CalculateMatchPredictions(Match match)
    {
        var predictions = match.Predictions.ToList();
        var rules = match.Championship.ScoringRules;

        var correctWinnerIds = predictions
            .Where(p => GetWinner(p.PredictedHome, p.PredictedAway) == GetWinner(match.HomeScore!.Value, match.AwayScore!.Value))
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
