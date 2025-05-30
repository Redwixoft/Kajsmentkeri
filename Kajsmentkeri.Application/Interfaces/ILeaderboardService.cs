using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Models;

namespace Kajsmentkeri.Application.Interfaces;

public interface ILeaderboardService
{
    Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(Guid championshipId);
    Task<LineGraphViewModel> GetLeaderboardProgressAsync(Guid championshipId);
}
