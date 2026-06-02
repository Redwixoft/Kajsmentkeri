using Kajsmentkeri.Domain;

namespace Kajsmentkeri.Application.Interfaces;

public interface IMatchService
{
    Task<Match> CreateMatchAsync(Guid championshipId, string homeTeam, string awayTeam, DateTime startTime, bool? isFinalMatch = null, bool? isBronzeMedalMatch = null);
    Task<List<Match>> GetMatchesByChampionshipAsync(Guid championshipId);
    Task<Match?> GetMatchByIdAsync(Guid matchId);
    Task UpdateMatchResultAsync(Guid matchId, int homeScore, int awayScore);
    Task UpdateMatchAsync(Guid matchId, string homeTeam, string awayTeam, DateTime startTime, bool? isFinalMatch, bool? isBronzeMedalMatch);
    Task RemoveMatchAsync(Guid matchId);
}
