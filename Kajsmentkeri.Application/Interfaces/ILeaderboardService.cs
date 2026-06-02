using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Models;
using Kajsmentkeri.Domain;

namespace Kajsmentkeri.Application.Interfaces;

public interface ILeaderboardService
{
    Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(Guid championshipId);
    void InvalidateLeaderboard(Guid championshipId);
    Task<List<LeaderboardEntryDto>> GetGlobalLeaderboardAsync(ChampionshipType? type = null);
    Task<LineGraphViewModel> GetLeaderboardProgressAsync(Guid championshipId);
    Task<Dictionary<Guid, List<(int Position, string ChampionshipName, int Year)>>> GetMedalCountsAsync(ChampionshipType? type = null);
    Task<ChampionshipRecordsDto> GetChampionshipRecordsAsync(ChampionshipType? type = null);
    Task<Dictionary<Guid, string?>> GetChampionshipWinnersAsync(IEnumerable<Guid> championshipIds);
    Task<GlobalStatsDto> GetGlobalStatsAsync(ChampionshipType? type = null);
    Task<Dictionary<Guid, UserPredictionProfileDto>> GetUserPredictionProfilesAsync(ChampionshipType? type = null);
    Task<UserPredictionProfileDto?> GetUserPredictionProfileAsync(Guid userId, ChampionshipType? type = null);
    Task<Dictionary<Guid, List<(string UserName, int Position)>>> GetChampionshipStandingsAsync(IEnumerable<Guid> championshipIds, Guid? currentUserId = null, int topN = 3);
    Task<Dictionary<Guid, (int TotalWinners, int TotalLuckers, int TotalOnlyOnes)>> GetChampionshipAggregatesAsync(IEnumerable<Guid> championshipIds);
    Task<ChampionshipRecordsDto> GetUserPersonalRecordsAsync(Guid userId, ChampionshipType? type = null);
    Task<List<UserChampionshipStatsDto>> GetUserChampionshipStatsAsync(Guid userId, ChampionshipType? type = null);
    LineGraphViewModel BuildLeaderboardProgress(
        IList<Match> scoredMatches,
        IList<Prediction> predictions,
        IReadOnlyDictionary<Guid, string> userNames);
    Task<(Guid? WinnerUserId, Guid? LoserUserId, string ChampionshipLabel)?> GetPreviousChampionshipExtremesAsync(Guid currentChampionshipId, ChampionshipType type);
}
