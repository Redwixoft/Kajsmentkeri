using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class MatchService : IMatchService
{
    private readonly AppDbContext _db;

    public MatchService(AppDbContext db)
    {
        _db = db;
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

        _db.Matches.Add(match);
        await _db.SaveChangesAsync();
    }

    public Task<Match?> GetMatchByIdAsync(Guid matchId)
    {
        return _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
    }

    public async Task<List<Match>> GetMatchesByChampionshipAsync(Guid championshipId)
    {
        return await _db.Matches
            .Where(m => m.ChampionshipId == championshipId)
            .OrderBy(m => m.StartTimeUtc)
            .ToListAsync();
    }

    public async Task UpdateMatchResultAsync(Guid matchId, int homeScore, int awayScore)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found");

        match.HomeScore = homeScore;
        match.AwayScore = awayScore;

        await _db.SaveChangesAsync();
    }

    public async Task RemoveMatchAsync(Guid matchId)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null) throw new InvalidOperationException("Match not found");
        _db.Matches.Remove(match);

        await _db.SaveChangesAsync();
    }
}
