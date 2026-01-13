using Kajsmentkeri.Domain;

namespace Kajsmentkeri.Application.Interfaces;

public interface IPredictionService
{
    Task SubmitPredictionAsync(Guid matchId, int predictedHome, int predictedAway);
    Task<DateTime> GetPredictionLockTimeAsync(Guid championshipId, Guid matchId, Guid userId);
    Task<List<Prediction>> GetPredictionsForChampionshipAsync(Guid championshipId);
    Task RemovePredictionsForMatchAsync(Guid matchId);
    Task RemovePredictionsForUserAsync(Guid userId);
}
