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

    public async Task<Championship> CreateChampionshipAsync(string name, int year, string? description)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
            throw new UnauthorizedAccessException("User must be logged in.");

        var championship = new Championship
        {
            Id = Guid.NewGuid(),
            Name = name,
            Year = year,
            Description = description,
            CreatedById = _currentUser.UserId.Value
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

        if (existing.ScoringRules != null && championship.ScoringRules != null)
        {
            existing.ScoringRules.PointsForCorrectWinner = championship.ScoringRules.PointsForCorrectWinner;
            existing.ScoringRules.PointsForExactScore = championship.ScoringRules.PointsForExactScore;
            existing.ScoringRules.PointsForOnlyCorrectWinner = championship.ScoringRules.PointsForOnlyCorrectWinner;
            existing.ScoringRules.RarityPointsBonus = championship.ScoringRules.RarityPointsBonus;
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

        // 4. Delete Championship
        context.Championships.Remove(championship);

        await context.SaveChangesAsync();
    }
}
