using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class ChampionshipService : IChampionshipService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ICurrentUserService _currentUser;

    public ChampionshipService(ICurrentUserService currentUser, IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _currentUser = currentUser;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<Championship>> GetAllAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Championships
            .Include(c => c.ScoringRules)
            .OrderByDescending(c => c.Year)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Championship> GetByIdAsync(Guid id)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var championship = await context.Championships
            .Include(c => c.ScoringRules)
            .FirstOrDefaultAsync(c => c.Id == id);

        return championship ?? throw new InvalidOperationException("Championship not found");
    }

    public async Task<Championship?> GetLatestAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Championships
            .OrderByDescending(c => c.Year)
            .ThenByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Championship> CreateChampionshipAsync(string name, int year, string? description, bool isTest = false)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
            throw new UnauthorizedAccessException("User must be logged in.");

        var championship = new Championship
        {
            Id = Guid.NewGuid(),
            Name = name,
            Year = year,
            Description = description,
            CreatedById = _currentUser.UserId.Value,
            IsTest = isTest
        };

        var scoringRules = new ChampionshipScoringRules
        {
            Id = Guid.NewGuid(),
            Championship = championship,
            PointsForCorrectWinner = 3,
            PointsForExactScore = 2,
            PointsForOnlyCorrectWinner = 2,
            RarityPointsBonus = 0,
            CreatedAt = DateTime.UtcNow
        };

        championship.ScoringRules = scoringRules;


        using var context = _dbContextFactory.CreateDbContext();
        context.Championships.Add(championship);
        await context.SaveChangesAsync();

        return championship;
    }

    public async Task<Championship> CreateChampionshipAsync(Championship championship)
    {
        using var context = _dbContextFactory.CreateDbContext();
        context.Championships.Add(championship);
        await context.SaveChangesAsync();

        return championship;
    }

    public async Task UpdateChampionshipAsync(Championship championship)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var existing = await context.Championships
            .Include(c => c.ScoringRules)
            .FirstOrDefaultAsync(c => c.Id == championship.Id);

        if (existing == null) throw new InvalidOperationException("Championship not found");

        existing.Name = championship.Name;
        existing.Year = championship.Year;
        existing.Description = championship.Description;
        existing.EnforceLeaderboardVisibilityRules = championship.EnforceLeaderboardVisibilityRules;
        existing.IsTest = championship.IsTest;
        existing.SupportsChampionshipWinnerPrediction = championship.SupportsChampionshipWinnerPrediction;
        existing.IsChampionshipEnded = championship.IsChampionshipEnded;
        existing.AllowHighConfidencePrediction = championship.AllowHighConfidencePrediction;
        existing.Type = championship.Type;

        if (existing.ScoringRules != null && championship.ScoringRules != null)
        {
            existing.ScoringRules.PointsForCorrectWinner = championship.ScoringRules.PointsForCorrectWinner;
            existing.ScoringRules.PointsForExactScore = championship.ScoringRules.PointsForExactScore;
            existing.ScoringRules.PointsForOnlyCorrectWinner = championship.ScoringRules.PointsForOnlyCorrectWinner;
            existing.ScoringRules.RarityPointsBonus = championship.ScoringRules.RarityPointsBonus;
            existing.ScoringRules.PointsForChampionshipWinner = championship.ScoringRules.PointsForChampionshipWinner;
            existing.ScoringRules.PointsForChampionshipRunnerUp = championship.ScoringRules.PointsForChampionshipRunnerUp;
            existing.ScoringRules.PointsForChampionshipThirdPlace = championship.ScoringRules.PointsForChampionshipThirdPlace;
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteChampionshipAsync(Guid id)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var championship = await context.Championships
            .Include(c => c.Matches)
                .ThenInclude(m => m.Predictions)
            .Include(c => c.ScoringRules)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (championship == null)
        {
            return;
        }

        // 1. Delete Predictions
        var predictions = championship.Matches.SelectMany(m => m.Predictions).ToList();
        if (predictions.Any())
        {
            context.Predictions.RemoveRange(predictions);
        }

        // 2. Delete Matches
        if (championship.Matches.Any())
        {
            context.Matches.RemoveRange(championship.Matches);
        }

        // 3. Delete Scoring Rules
        if (championship.ScoringRules != null)
        {
            context.ChampionshipScoringRules.Remove(championship.ScoringRules);
        }
        
        // 4. Delete Winner Predictions
        var winnerPredictions = await context.ChampionshipWinnerPredictions.Where(p => p.ChampionshipId == id).ToListAsync();
        if (winnerPredictions.Any())
        {
            context.ChampionshipWinnerPredictions.RemoveRange(winnerPredictions);
        }

        // 5. Delete Championship
        context.Championships.Remove(championship);

        await context.SaveChangesAsync();
    }

    public async Task EndChampionshipAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var championship = await context.Championships
            .Include(c => c.ScoringRules)
            .FirstOrDefaultAsync(c => c.Id == championshipId);

        if (championship == null) throw new InvalidOperationException("Championship not found");
        if (championship.IsChampionshipEnded) throw new InvalidOperationException("Championship already ended");
        if (!championship.SupportsChampionshipWinnerPrediction) throw new InvalidOperationException("This championship does not support winner predictions");

        var matches = await context.Matches
            .Where(m => m.ChampionshipId == championshipId && m.HomeScore.HasValue && m.AwayScore.HasValue)
            .OrderByDescending(m => m.StartTimeUtc)
            .Take(2)
            .ToListAsync();

        if (matches.Count < 2) throw new InvalidOperationException("Not enough finished matches to determine winners (need at least 2: final and 3rd place game)");

        var finalMatch = matches[0];
        var bronzeMatch = matches[1];

        string goldTeam = finalMatch.HomeScore > finalMatch.AwayScore ? finalMatch.HomeTeam : finalMatch.AwayTeam;
        string silverTeam = finalMatch.HomeScore > finalMatch.AwayScore ? finalMatch.AwayTeam : finalMatch.HomeTeam;
        string bronzeTeam = bronzeMatch.HomeScore > bronzeMatch.AwayScore ? bronzeMatch.HomeTeam : bronzeMatch.AwayTeam;

        var predictions = await context.ChampionshipWinnerPredictions
            .Where(p => p.ChampionshipId == championshipId)
            .ToListAsync();

        foreach (var pred in predictions)
        {
            if (pred.TeamName == goldTeam)
                pred.PointsAwarded = championship.ScoringRules.PointsForChampionshipWinner;
            else if (pred.TeamName == silverTeam)
                pred.PointsAwarded = championship.ScoringRules.PointsForChampionshipRunnerUp;
            else if (pred.TeamName == bronzeTeam)
                pred.PointsAwarded = championship.ScoringRules.PointsForChampionshipThirdPlace;
            else
                pred.PointsAwarded = 0;
        }

        championship.IsChampionshipEnded = true;
        await context.SaveChangesAsync();
    }

    public async Task<bool> IsParticipatingAsync(Guid championshipId, Guid userId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.ChampionshipParticipations
            .AnyAsync(p => p.ChampionshipId == championshipId && p.UserId == userId);
    }

    public async Task JoinChampionshipAsync(Guid championshipId, Guid userId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var alreadyJoined = await context.ChampionshipParticipations
            .AnyAsync(p => p.ChampionshipId == championshipId && p.UserId == userId);
        if (alreadyJoined) return;

        context.ChampionshipParticipations.Add(new ChampionshipParticipation
        {
            Id = Guid.NewGuid(),
            ChampionshipId = championshipId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    public async Task<List<Guid>> GetParticipantUserIdsAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.ChampionshipParticipations
            .Where(p => p.ChampionshipId == championshipId)
            .Select(p => p.UserId)
            .ToListAsync();
    }

    public async Task UpdateWinnerPaymentInfoAsync(Guid championshipId, string iban, string note)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
            throw new UnauthorizedAccessException("User must be logged in.");

        using var context = _dbContextFactory.CreateDbContext();
        
        var championship = await context.Championships
            .Include(c => c.ScoringRules)
            .FirstOrDefaultAsync(c => c.Id == championshipId);
            
        if (championship == null)
            throw new InvalidOperationException("Championship not found");
            
        if (!championship.IsChampionshipEnded)
            throw new InvalidOperationException("Championship has not ended yet");

        // Get leaderboard to determine winner
        var leaderboard = await context.Predictions
            .Where(p => p.Match.ChampionshipId == championshipId)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, TotalPoints = g.Sum(p => p.Points) })
            .OrderByDescending(x => x.TotalPoints)
            .ToListAsync();

        // Add winner prediction points
        var winnerPredictions = await context.ChampionshipWinnerPredictions
            .Where(p => p.ChampionshipId == championshipId)
            .ToListAsync();

        var leaderboardWithWinnerPoints = leaderboard.Select(entry =>
        {
            var winnerPred = winnerPredictions.FirstOrDefault(p => p.UserId == entry.UserId);
            return new
            {
                entry.UserId,
                TotalPoints = entry.TotalPoints + (winnerPred?.PointsAwarded ?? 0)
            };
        }).OrderByDescending(x => x.TotalPoints).ToList();

        if (leaderboardWithWinnerPoints.Count == 0)
            throw new InvalidOperationException("No leaderboard data available");

        var winnerId = leaderboardWithWinnerPoints.First().UserId;
        
        if (winnerId != _currentUser.UserId.Value)
            throw new UnauthorizedAccessException("Only the championship winner can submit payment information");

        championship.WinnerIban = iban;
        championship.WinnerNote = note;
        await context.SaveChangesAsync();
    }
}
