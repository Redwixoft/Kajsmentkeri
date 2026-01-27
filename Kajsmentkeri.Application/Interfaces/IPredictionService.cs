using Kajsmentkeri.Domain;

namespace Kajsmentkeri.Application.Interfaces;

public interface IPredictionService
{
    Task SubmitPredictionAsync(Guid matchId, int predictedHome, int predictedAway);
    Task SetPredictionAsync(Guid matchId, Guid userId, int predictedHome, int predictedAway);
    Task<DateTime> GetPredictionLockTimeAsync(Guid championshipId, Guid matchId, Guid userId);
    Task<List<Prediction>> GetPredictionsForChampionshipAsync(Guid championshipId);
    Task RemovePredictionsForMatchAsync(Guid matchId);
    Task RemovePredictionsForUserAsync(Guid userId);
    Task<List<PredictionAuditLog>> GetAuditLogsForMatchAsync(Guid matchId);
    
    Task SubmitWinnerPredictionAsync(Guid championshipId, string teamName);
    Task<ChampionshipWinnerPrediction?> GetWinnerPredictionAsync(Guid championshipId, Guid userId);
    Task<List<ChampionshipWinnerPrediction>> GetWinnerPredictionsForChampionshipAsync(Guid championshipId);
}
