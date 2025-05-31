using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kajsmentkeri.Application.Services;

public class PredictionService : IPredictionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILeaderboardService _leaderboardService;
    private readonly ILogger<PredictionService> _logger;

    public PredictionService(AppDbContext db, ICurrentUserService currentUser, ILeaderboardService leaderboardService, ILogger<PredictionService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _leaderboardService = leaderboardService;
        _logger = logger;
    }

    public async Task SubmitPredictionAsync(Guid matchId, int predictedHome, int predictedAway)
    {
        _logger.LogInformation($"{nameof(SubmitPredictionAsync)} start: {DateTime.Now}");

        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
            throw new UnauthorizedAccessException("User must be logged in.");

        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found");

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

        _logger.LogInformation($"{nameof(SubmitPredictionAsync)} end: {DateTime.Now}");
    }

    public async Task<DateTime> GetPredictionLockTimeAsync(Guid championshipId, Guid matchId, Guid userId)
    {
        _logger.LogInformation($"{nameof(GetPredictionLockTimeAsync)} start: {DateTime.Now}");

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

        _logger.LogInformation($"{nameof(GetPredictionLockTimeAsync)} end: {DateTime.Now}");
        return matchStart;
    }

    public Task<List<Prediction>> GetPredictionsForChampionshipAsync(Guid championshipId)
    {
        return _db.Predictions
            .Where(p => p.Match.ChampionshipId == championshipId)
            .ToListAsync();
    }
}
