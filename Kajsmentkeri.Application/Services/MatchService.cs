using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class MatchService : IMatchService
{
    private readonly AppDbContext _db;
    private readonly IPredictionScoringService _scoringService;

    public MatchService(AppDbContext db, IPredictionScoringService scoringService)
    {
        _db = db;
        _scoringService = scoringService;
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

    public async Task UpdateMatchResultAsync(Guid matchId, int homeScore, int awayScore)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) throw new InvalidOperationException("Match not found");

        match.HomeScore = homeScore;
        match.AwayScore = awayScore;

        await _db.SaveChangesAsync();

        // Now that the result is known, recalculate all predictions
        await _scoringService.RecalculateForMatchAsync(matchId);
    }
}
