using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class MatchService : IMatchService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ITimeService _timeService;

    public MatchService(IDbContextFactory<AppDbContext> dbContextFactory, ITimeService timeService)
    {
        _dbContextFactory = dbContextFactory;
        _timeService = timeService;
    }

    public async Task CreateMatchAsync(Guid championshipId, string homeTeam, string awayTeam, DateTime startTime)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            ChampionshipId = championshipId,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            StartTimeUtc = _timeService.ToUtc(startTime)
        };

        using var context = _dbContextFactory.CreateDbContext();
        context.Matches.Add(match);
        await context.SaveChangesAsync();
    }

    public async Task<Match?> GetMatchByIdAsync(Guid matchId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
    }

    public async Task<List<Match>> GetMatchesByChampionshipAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Matches
            .Include(m => m.Predictions)
            .Where(m => m.ChampionshipId == championshipId)
            .OrderBy(m => m.StartTimeUtc)
            .ToListAsync();
    }

    public async Task UpdateMatchResultAsync(Guid matchId, int homeScore, int awayScore)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found");

        match.HomeScore = homeScore;
        match.AwayScore = awayScore;

        await context.SaveChangesAsync();
    }

    public async Task UpdateMatchAsync(Guid matchId, string homeTeam, string awayTeam, DateTime startTime)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found");

        match.HomeTeam = homeTeam;
        match.AwayTeam = awayTeam;
        match.StartTimeUtc = _timeService.ToUtc(startTime);

        await context.SaveChangesAsync();
    }

    public async Task RemoveMatchAsync(Guid matchId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var match = await context.Matches
            .Include(m => m.Predictions)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null) throw new InvalidOperationException("Match not found");
        
        if (match.Predictions.Any())
        {
            context.Predictions.RemoveRange(match.Predictions);
        }
        
        context.Matches.Remove(match);

        await context.SaveChangesAsync();
    }
}
