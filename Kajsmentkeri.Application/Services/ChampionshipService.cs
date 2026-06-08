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
            .AsNoTracking()
            .Include(c => c.ScoringRules)
            .OrderByDescending(c => c.Year)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Championship> GetByIdAsync(Guid id)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var championship = await context.Championships
            .AsNoTracking()
            .Include(c => c.ScoringRules)
            .FirstOrDefaultAsync(c => c.Id == id);

        return championship ?? throw new InvalidOperationException("Championship not found");
    }

    public async Task<Championship?> GetLatestAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Championships
            .AsNoTracking()
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
        existing.IsDrawEnabled = championship.IsDrawEnabled;
        existing.Type = championship.Type;
        existing.EntryFee = championship.EntryFee;
        existing.RunnerUpPaysFree = championship.RunnerUpPaysFree;
        existing.LastPlacePaysDouble = championship.LastPlacePaysDouble;

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

        // 2. Delete Safe Locks
        var matchIds = championship.Matches.Select(m => m.Id).ToList();
        if (matchIds.Any())
        {
            var safeLocks = await context.SafeLocks.Where(sl => matchIds.Contains(sl.MatchId)).ToListAsync();
            if (safeLocks.Any())
                context.SafeLocks.RemoveRange(safeLocks);
        }

        // 3. Delete Matches
        if (championship.Matches.Any())
        {
            context.Matches.RemoveRange(championship.Matches);
        }

        // 4. Delete Scoring Rules
        if (championship.ScoringRules != null)
        {
            context.ChampionshipScoringRules.Remove(championship.ScoringRules);
        }
        
        // 5. Delete Winner Predictions
        var winnerPredictions = await context.ChampionshipWinnerPredictions.Where(p => p.ChampionshipId == id).ToListAsync();
        if (winnerPredictions.Any())
        {
            context.ChampionshipWinnerPredictions.RemoveRange(winnerPredictions);
        }

        // 6. Delete Participations
        var participations = await context.ChampionshipParticipations.Where(p => p.ChampionshipId == id).ToListAsync();
        if (participations.Any())
        {
            context.ChampionshipParticipations.RemoveRange(participations);
        }

        // 7. Delete Championship
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

    public async Task RecalculateChampionshipAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var championship = await context.Championships
            .Include(c => c.ScoringRules)
            .FirstOrDefaultAsync(c => c.Id == championshipId);

        if (championship == null) throw new InvalidOperationException("Championship not found");
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

    public async Task<Championship> CopyChampionshipAsync(Guid id)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
            throw new UnauthorizedAccessException("User must be logged in.");

        using var context = _dbContextFactory.CreateDbContext();

        var source = await context.Championships
            .Include(c => c.ScoringRules)
            .Include(c => c.Matches)
                .ThenInclude(m => m.Predictions)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException("Championship not found");

        var safeLocks = await context.SafeLocks
            .Where(sl => source.Matches.Select(m => m.Id).Contains(sl.MatchId))
            .ToListAsync();

        var winnerPredictions = await context.ChampionshipWinnerPredictions
            .Where(p => p.ChampionshipId == id)
            .ToListAsync();

        var participations = await context.ChampionshipParticipations
            .Where(p => p.ChampionshipId == id)
            .ToListAsync();

        var newChampionshipId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var newChampionship = new Championship
        {
            Id = newChampionshipId,
            Name = source.Name + " - COPY",
            Year = source.Year,
            Description = source.Description,
            EnforceLeaderboardVisibilityRules = source.EnforceLeaderboardVisibilityRules,
            SupportsChampionshipWinnerPrediction = source.SupportsChampionshipWinnerPrediction,
            IsChampionshipEnded = source.IsChampionshipEnded,
            IsTest = true,
            AllowHighConfidencePrediction = source.AllowHighConfidencePrediction,
            Type = source.Type,
            EntryFee = source.EntryFee,
            RunnerUpPaysFree = source.RunnerUpPaysFree,
            LastPlacePaysDouble = source.LastPlacePaysDouble,
            IsDrawEnabled = source.IsDrawEnabled,
            CreatedById = _currentUser.UserId.Value,
            CreatedAt = now,
        };

        context.Championships.Add(newChampionship);

        var newScoringRules = new ChampionshipScoringRules
        {
            Id = Guid.NewGuid(),
            ChampionshipId = newChampionshipId,
            PointsForCorrectWinner = source.ScoringRules.PointsForCorrectWinner,
            PointsForExactScore = source.ScoringRules.PointsForExactScore,
            PointsForOnlyCorrectWinner = source.ScoringRules.PointsForOnlyCorrectWinner,
            RarityPointsBonus = source.ScoringRules.RarityPointsBonus,
            PointsForChampionshipWinner = source.ScoringRules.PointsForChampionshipWinner,
            PointsForChampionshipRunnerUp = source.ScoringRules.PointsForChampionshipRunnerUp,
            PointsForChampionshipThirdPlace = source.ScoringRules.PointsForChampionshipThirdPlace,
            CreatedAt = now,
        };

        context.ChampionshipScoringRules.Add(newScoringRules);

        // Build old match ID → new match ID map for remapping predictions and safe locks
        var matchIdMap = new Dictionary<Guid, Guid>();

        foreach (var match in source.Matches)
        {
            var newMatchId = Guid.NewGuid();
            matchIdMap[match.Id] = newMatchId;

            var newMatch = new Match
            {
                Id = newMatchId,
                ChampionshipId = newChampionshipId,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                StartTimeUtc = match.StartTimeUtc,
                HomeScore = match.HomeScore,
                AwayScore = match.AwayScore,
                IsFinalMatch = match.IsFinalMatch,
                IsBronzeMedalMatch = match.IsBronzeMedalMatch,
            };

            context.Matches.Add(newMatch);

            foreach (var prediction in match.Predictions)
            {
                context.Predictions.Add(new Prediction
                {
                    Id = Guid.NewGuid(),
                    MatchId = newMatchId,
                    UserId = prediction.UserId,
                    PredictedHome = prediction.PredictedHome,
                    PredictedAway = prediction.PredictedAway,
                    Points = prediction.Points,
                    GotWinner = prediction.GotWinner,
                    GotExactScore = prediction.GotExactScore,
                    OneGoalMiss = prediction.OneGoalMiss,
                    IsOnlyCorrect = prediction.IsOnlyCorrect,
                    RarityPart = prediction.RarityPart,
                    IsHighConfidence = prediction.IsHighConfidence,
                });
            }
        }

        foreach (var sl in safeLocks)
        {
            context.SafeLocks.Add(new SafeLock
            {
                Id = Guid.NewGuid(),
                MatchId = matchIdMap[sl.MatchId],
                OwnerUserId = sl.OwnerUserId,
                TrackedUserId = sl.TrackedUserId,
                HomeWinPredictedHome = sl.HomeWinPredictedHome,
                HomeWinPredictedAway = sl.HomeWinPredictedAway,
                DrawPredictedHome = sl.DrawPredictedHome,
                DrawPredictedAway = sl.DrawPredictedAway,
                AwayWinPredictedHome = sl.AwayWinPredictedHome,
                AwayWinPredictedAway = sl.AwayWinPredictedAway,
                CreatedAt = sl.CreatedAt,
                LastTriggeredAt = sl.LastTriggeredAt,
            });
        }

        foreach (var wp in winnerPredictions)
        {
            context.ChampionshipWinnerPredictions.Add(new ChampionshipWinnerPrediction
            {
                Id = Guid.NewGuid(),
                ChampionshipId = newChampionshipId,
                UserId = wp.UserId,
                TeamName = wp.TeamName,
                PointsAwarded = wp.PointsAwarded,
                CreatedAt = wp.CreatedAt,
            });
        }

        foreach (var p in participations)
        {
            context.ChampionshipParticipations.Add(new ChampionshipParticipation
            {
                Id = Guid.NewGuid(),
                ChampionshipId = newChampionshipId,
                UserId = p.UserId,
                JoinedAt = p.JoinedAt,
            });
        }

        await context.SaveChangesAsync();

        return newChampionship;
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
