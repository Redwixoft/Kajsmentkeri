using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Models;
using Kajsmentkeri.Domain;

namespace Kajsmentkeri.Application.Interfaces;

public interface ILeaderboardService
{
    Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(Guid championshipId);
    Task<List<LeaderboardEntryDto>> GetGlobalLeaderboardAsync(ChampionshipType? type = null);
    Task<LineGraphViewModel> GetLeaderboardProgressAsync(Guid championshipId);
    Task<Dictionary<Guid, List<(int Position, string ChampionshipName, int Year)>>> GetMedalCountsAsync(ChampionshipType? type = null);
    Task<ChampionshipRecordsDto> GetChampionshipRecordsAsync(ChampionshipType? type = null);
    Task<Dictionary<Guid, string?>> GetChampionshipWinnersAsync(IEnumerable<Guid> championshipIds);
    LineGraphViewModel BuildLeaderboardProgress(
        IList<Match> scoredMatches,
        IList<Prediction> predictions,
        IReadOnlyDictionary<Guid, string> userNames);
}
