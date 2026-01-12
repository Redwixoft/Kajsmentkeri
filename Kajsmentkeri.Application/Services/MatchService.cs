using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class MatchService : IMatchService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public MatchService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task CreateMatchAsync(Guid championshipId, string homeTeam, string awayTeam, DateTime startTime)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            ChampionshipId = championshipId,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            StartTimeUtc = startTime.ToUniversalTime()
        };

        using var context = _dbContextFactory.CreateDbContext();
        context.Matches.Add(match);
        await context.SaveChangesAsync();
    }

    public Task<Match?> GetMatchByIdAsync(Guid matchId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
    }

    public async Task<List<Match>> GetMatchesByChampionshipAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Matches
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

    public async Task RemoveMatchAsync(Guid matchId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null) throw new InvalidOperationException("Match not found");
        context.Matches.Remove(match);

        await context.SaveChangesAsync();
    }
}
