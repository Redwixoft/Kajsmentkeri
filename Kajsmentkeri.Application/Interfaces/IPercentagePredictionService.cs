using Kajsmentkeri.Application.DTOs;

namespace Kajsmentkeri.Application.Interfaces;

public interface IPercentagePredictionService
{
    const int QuestionCount = 10;

    Task<bool> HasUserSubmittedAsync(Guid userId);
    Task SubmitPredictionsAsync(Guid userId, PercentagePredictionDto dto);

    // Recall round: "what do you think you answered a year ago?"
    Task<PercentageRecallStatusDto> GetRecallStatusAsync(Guid userId);
    Task SubmitRecallAsync(Guid userId, IReadOnlyList<int> answers);

    /// <summary>Outcome per question number (1..QuestionCount); null = not yet known.</summary>
    Task<IReadOnlyDictionary<int, bool?>> GetOutcomesAsync();
    Task SetOutcomesAsync(IReadOnlyDictionary<int, bool?> outcomes);

    Task<PercentagePredictionConclusionDto> GetConclusionAsync();
}
