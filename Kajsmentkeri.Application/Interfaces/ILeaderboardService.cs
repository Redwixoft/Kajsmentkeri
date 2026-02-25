using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Models;

namespace Kajsmentkeri.Application.Interfaces;

public interface ILeaderboardService
{
    Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(Guid championshipId);
    Task<List<LeaderboardEntryDto>> GetGlobalLeaderboardAsync();
    Task<LineGraphViewModel> GetLeaderboardProgressAsync(Guid championshipId);
    Task<Dictionary<Guid, List<(int Position, string ChampionshipName, int Year)>>> GetMedalCountsAsync();
}
