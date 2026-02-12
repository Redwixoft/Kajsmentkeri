using Kajsmentkeri.Domain;

namespace Kajsmentkeri.Application.Interfaces;

public interface IMatchService
{
    Task<Match> CreateMatchAsync(Guid championshipId, string homeTeam, string awayTeam, DateTime startTime);
    Task<List<Match>> GetMatchesByChampionshipAsync(Guid championshipId);
    Task<Match?> GetMatchByIdAsync(Guid matchId);
    Task UpdateMatchResultAsync(Guid matchId, int homeScore, int awayScore);
    Task UpdateMatchAsync(Guid matchId, string homeTeam, string awayTeam, DateTime startTime);
    Task RemoveMatchAsync(Guid matchId);
}
