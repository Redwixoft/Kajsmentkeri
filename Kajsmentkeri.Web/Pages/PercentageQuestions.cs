using Kajsmentkeri.Application.Interfaces;

namespace Kajsmentkeri.Web.Pages;

/// <summary>Reads the percentage-prediction question texts from configuration, keyed by question number (1-based).</summary>
public static class PercentageQuestions
{
    public static IReadOnlyDictionary<int, string> Load(IConfiguration configuration)
    {
        var section = configuration.GetSection("PercentagePredictions:Questions");
        return Enumerable.Range(1, IPercentagePredictionService.QuestionCount)
            .ToDictionary(q => q, q => section[$"Question{q}"] ?? $"Question {q}");
    }
}
