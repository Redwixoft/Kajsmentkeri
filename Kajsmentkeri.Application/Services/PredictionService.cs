using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;

namespace Kajsmentkeri.Application.Services;

public class PredictionService : IPredictionService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly ILeaderboardService _leaderboardService;
    private readonly IServiceScopeFactory _scopeFactory;

    public PredictionService(ICurrentUserService currentUser, ILeaderboardService leaderboardService, IDbContextFactory<AppDbContext> dbContextFactory, IServiceScopeFactory scopeFactory)
    {
        _currentUser = currentUser;
        _leaderboardService = leaderboardService;
        _dbContextFactory = dbContextFactory;
        _scopeFactory = scopeFactory;
    }

    public async Task SubmitPredictionAsync(Guid matchId, int predictedHome, int predictedAway)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
            throw new UnauthorizedAccessException("User must be logged in.");

        await SetPredictionInternalAsync(matchId, _currentUser.UserId.Value, predictedHome, predictedAway, checkLock: true);
    }

    public async Task SetPredictionAsync(Guid matchId, Guid userId, int predictedHome, int predictedAway)
    {
        await SetPredictionInternalAsync(matchId, userId, predictedHome, predictedAway, checkLock: false);
    }

    private async Task SetPredictionInternalAsync(Guid matchId, Guid userId, int predictedHome, int predictedAway, bool checkLock)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found");

        if (checkLock)
        {
            var lockTime = await GetPredictionLockTimeAsync(match.ChampionshipId, matchId, userId);
            if (DateTime.UtcNow > lockTime)
            {
                throw new InvalidOperationException("Prediction for this match is already locked.");
            }
        }

        var prediction = await context.Predictions
            .FirstOrDefaultAsync(p => p.MatchId == matchId && p.UserId == userId);

        // Capture old values for audit log
        int? oldHome = prediction?.PredictedHome;
        int? oldAway = prediction?.PredictedAway;

        if (prediction == null)
        {
            prediction = new Prediction
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UserId = userId
            };
            context.Predictions.Add(prediction);
        }

        prediction.PredictedHome = predictedHome;
        prediction.PredictedAway = predictedAway;

        // If it's an admin update (no lock check), log it
        if (!checkLock && _currentUser.UserId != null && _currentUser.UserId != Guid.Empty)
        {
            // Only log if something changed or it's new
            if (oldHome != predictedHome || oldAway != predictedAway)
            {
                using var scope = _scopeFactory.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var targetUser = await userManager.FindByIdAsync(userId.ToString());

                var log = new PredictionAuditLog
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId,
                    AdminId = _currentUser.UserId.Value,
                    AdminName = _currentUser.UserName ?? "Admin",
                    TargetUserId = userId,
                    TargetUserName = targetUser?.UserName ?? "User",
                    OldHomeScore = oldHome,
                    OldAwayScore = oldAway,
                    NewHomeScore = predictedHome,
                    NewAwayScore = predictedAway,
                    TimestampUtc = DateTime.UtcNow,
                    MatchSummary = $"{match.HomeTeam} - {match.AwayTeam}"
                };
                context.PredictionAuditLogs.Add(log);
            }
        }

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
            .Take(8);

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

    public async Task<List<PredictionAuditLog>> GetAuditLogsForMatchAsync(Guid matchId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.PredictionAuditLogs
            .Where(l => l.MatchId == matchId)
            .OrderByDescending(l => l.TimestampUtc)
            .ToListAsync();
    }
}
