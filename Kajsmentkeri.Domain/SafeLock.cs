namespace Kajsmentkeri.Domain;

public class SafeLock
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;

    public Guid OwnerUserId { get; set; }
    public Guid TrackedUserId { get; set; }

    public int HomeWinPredictedHome { get; set; }
    public int HomeWinPredictedAway { get; set; }

    public int? DrawPredictedHome { get; set; }
    public int? DrawPredictedAway { get; set; }

    public int AwayWinPredictedHome { get; set; }
    public int AwayWinPredictedAway { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
}
