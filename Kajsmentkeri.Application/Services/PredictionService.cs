using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class PredictionService : IPredictionService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly ILeaderboardService _leaderboardService;

    public PredictionService(ICurrentUserService currentUser, ILeaderboardService leaderboardService, IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _currentUser = currentUser;
        _leaderboardService = leaderboardService;
        _dbContextFactory = dbContextFactory;
    }

    public async Task SubmitPredictionAsync(Guid matchId, int predictedHome, int predictedAway)
    {
        using var context = _dbContextFactory.CreateDbContext();


        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
            throw new UnauthorizedAccessException("User must be logged in.");

        var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found");

        var prediction = await context.Predictions
            .FirstOrDefaultAsync(p => p.MatchId == matchId && p.UserId == _currentUser.UserId);

        if (prediction == null)
        {
            prediction = new Prediction
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UserId = _currentUser.UserId.Value
            };
            context.Predictions.Add(prediction);
        }

        prediction.PredictedHome = predictedHome;
        prediction.PredictedAway = predictedAway;

        await context.SaveChangesAsync();
    }

    public async Task<DateTime> GetPredictionLockTimeAsync(Guid championshipId, Guid matchId, Guid userId)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null)
            throw new Exception("Match not found");

        var matchStart = match.StartTimeUtc;

        // If it's in the first matches in the championship
        var firstMatches = context.Matches
            .Where(m => m.ChampionshipId == championshipId)
            .OrderBy(m => m.StartTimeUtc)
            .Take(2);

        if (firstMatches.Select(m => m.Id).Contains(match.Id))
            return matchStart;

        // Load leaderboard
        var leaderboard = await _leaderboardService.GetLeaderboardAsync(championshipId);
        if (leaderboard.Count == 0)
            return matchStart;

        var first = leaderboard.First().UserId;
        var last = leaderboard.Last().UserId;

        if (userId == first)
            return matchStart.AddMinutes(-10);

        if (userId == last)
            return matchStart.AddMinutes(5);


        return matchStart;
    }

    public async Task<List<Prediction>> GetPredictionsForChampionshipAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Predictions
            .Where(p => p.Match.ChampionshipId == championshipId)
            .ToListAsync();
    }

    public async Task RemovePredictionsForMatchAsync(Guid matchId)
    {
        using var context = _dbContextFactory.CreateDbContext();


        var predictions = await context.Predictions
            .Where(p => p.MatchId == matchId)
            .ToListAsync();
        if (predictions.Count > 0)
        {
            context.Predictions.RemoveRange(predictions);
            await context.SaveChangesAsync();
        }


    }

    public async Task RemovePredictionsForUserAsync(Guid userId)
    {
        using var context = _dbContextFactory.CreateDbContext();


        var predictions = await context.Predictions
            .Where(p => p.UserId == userId)
            .ToListAsync();

        if (predictions.Any())
        {
            context.Predictions.RemoveRange(predictions);
            await context.SaveChangesAsync();
        }


    }
}
