namespace Kajsmentkeri.Application.DTOs;

public record PercentageRecallStatusDto(
    bool HasOriginal,
    bool HasRecall,
    bool IsConclusionRevealed);

public record PercentageUserAnswerDto(
    string UserName,
    int Original,
    int Recalled,
    // Recalled − Original (positive = remembered higher than actually answered).
    int Difference,
    // Points for the original guess; null while the outcome is unknown.
    int? Points);

public record PercentageQuestionResultDto(
    int QuestionNumber,
    bool? Happened,
    IReadOnlyList<PercentageUserAnswerDto> Answers);

public record PercentagePredictorRankDto(
    int Rank,
    string UserName,
    int TotalPoints,
    // Sum of |recalled − original| over all questions.
    int TotalAbsoluteDifference);

public class PercentagePredictionConclusionDto
{
    public bool IsRevealed { get; init; }
    public int TotalParticipants { get; init; }
    public int SubmittedRecalls { get; init; }
    public IReadOnlyList<string> PendingUserNames { get; init; } = Array.Empty<string>();
    public int KnownOutcomeCount { get; init; }
    public IReadOnlyList<PercentagePredictorRankDto> Ranking { get; init; } = Array.Empty<PercentagePredictorRankDto>();
    public IReadOnlyList<PercentageQuestionResultDto> Questions { get; init; } = Array.Empty<PercentageQuestionResultDto>();
}
