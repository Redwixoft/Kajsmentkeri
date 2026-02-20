using Kajsmentkeri.Application.DTOs;

namespace Kajsmentkeri.Application.Interfaces;

public interface IPercentagePredictionService
{
    Task<bool> HasUserSubmittedAsync(Guid userId);
    Task SubmitPredictionsAsync(Guid userId, PercentagePredictionDto dto);
}
