namespace Kajsmentkeri.Domain;

public class Prediction
{
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;

    public Guid UserId { get; set; }

    public int PredictedHome { get; set; }
    public int PredictedAway { get; set; }

    // Computed stats
    public int Points { get; set; }
    public bool GotWinner { get; set; }
    public bool GotExactScore { get; set; }
    public bool OneGoalMiss { get; set; }
    public bool IsOnlyCorrect { get; set; }
    public decimal RarityPart { get; set; }
    public bool IsHighConfidence { get; set; }

    public override string ToString()
    {
        return PredictedHome + ":" + PredictedAway;
    }
}
