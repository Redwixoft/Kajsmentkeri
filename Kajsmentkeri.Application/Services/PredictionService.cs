using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class PredictionService : IPredictionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILeaderboardService _leaderboardService;

    public PredictionService(AppDbContext db, ICurrentUserService currentUser, ILeaderboardService leaderboardService)
    {
        _db = db;
        _currentUser = currentUser;
        _leaderboardService = leaderboardService;
    }

    public async Task SubmitPredictionAsync(Guid matchId, int predictedHome, int predictedAway)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
            throw new UnauthorizedAccessException("User must be logged in.");

        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found");

        if (match.StartTimeUtc <= DateTime.UtcNow)
            throw new InvalidOperationException("Cannot predict match after it started");

        var prediction = await _db.Predictions
            .FirstOrDefaultAsync(p => p.MatchId == matchId && p.UserId == _currentUser.UserId);

        if (prediction == null)
        {
            prediction = new Prediction
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UserId = _currentUser.UserId.Value
            };
            _db.Predictions.Add(prediction);
        }

        prediction.PredictedHome = predictedHome;
        prediction.PredictedAway = predictedAway;

        await _db.SaveChangesAsync();
    }

    public async Task<DateTime> GetPredictionLockTimeAsync(Guid championshipId, Guid matchId, Guid userId)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null)
            throw new Exception("Match not found");

        var matchStart = match.StartTimeUtc;

        // If it's in the first matches in the championship
        var firstMatches = _db.Matches
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

}
