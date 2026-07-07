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

    public async Task<Match> CreateMatchAsync(Guid championshipId, string homeTeam, string awayTeam, DateTime startTime, bool? isFinalMatch = null, bool? isBronzeMedalMatch = null, bool lineAfter = false)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            ChampionshipId = championshipId,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            IsFinalMatch = isFinalMatch,
            IsBronzeMedalMatch = isBronzeMedalMatch,
            LineAfter = lineAfter
        };
        match.StartTimeUtc = _timeService.ToUtc(startTime);

        using var context = _dbContextFactory.CreateDbContext();
        context.Matches.Add(match);
        await context.SaveChangesAsync();
        return match;
    }

    public async Task<Match?> GetMatchByIdAsync(Guid matchId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.Id == matchId);
    }

    public async Task<List<Match>> GetMatchesByChampionshipAsync(Guid championshipId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Matches
            .AsNoTracking()
            .Include(m => m.Predictions)
            .Where(m => m.ChampionshipId == championshipId)
            .OrderBy(m => m.StartTimeUtc)
            .ToListAsync();
    }

    public async Task UpdateMatchResultAsync(Guid matchId, int homeScore, int awayScore, Guid? actorId = null, string? actorName = null)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found");

        var oldHome = match.HomeScore;
        var oldAway = match.AwayScore;

        match.HomeScore = homeScore;
        match.AwayScore = awayScore;

        if (actorId != null && (oldHome != homeScore || oldAway != awayScore))
        {
            context.PredictionAuditLogs.Add(new PredictionAuditLog
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                AdminId = actorId.Value,
                AdminName = actorName ?? "User",
                OldHomeScore = oldHome,
                OldAwayScore = oldAway,
                NewHomeScore = homeScore,
                NewAwayScore = awayScore,
                TimestampUtc = _timeService.UtcNow,
                IsResultUpdate = true,
                MatchSummary = $"{match.HomeTeam} - {match.AwayTeam}"
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task UpdateMatchAsync(Guid matchId, string homeTeam, string awayTeam, DateTime startTime, bool? isFinalMatch, bool? isBronzeMedalMatch, bool lineAfter = false)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found");

        match.HomeTeam = homeTeam;
        match.AwayTeam = awayTeam;
        match.StartTimeUtc = _timeService.ToUtc(startTime);
        match.IsFinalMatch = isFinalMatch;
        match.IsBronzeMedalMatch = isBronzeMedalMatch;
        match.LineAfter = lineAfter;

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
