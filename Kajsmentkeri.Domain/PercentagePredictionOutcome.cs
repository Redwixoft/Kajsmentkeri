namespace Kajsmentkeri.Domain;

/// <summary>
/// Whether the event behind a percentage-prediction question actually happened during the year.
/// Keyed by question number (1–10). <see cref="Happened"/> is null while the outcome is unknown.
/// </summary>
public class PercentagePredictionOutcome
{
    public int QuestionNumber { get; set; }
    public bool? Happened { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
