
namespace Kajsmentkeri.Application.Interfaces;

public interface IMatchService
{
    Task CreateMatchAsync(Guid championshipId, string homeTeam, string awayTeam, DateTime startTime);
    Task UpdateMatchResultAsync(Guid matchId, int homeScore, int awayScore);
}
