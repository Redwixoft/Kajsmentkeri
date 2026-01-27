namespace Kajsmentkeri.Domain;

public class ChampionshipWinnerPrediction
{
    public Guid Id { get; set; }
    public Guid ChampionshipId { get; set; }
    public Championship Championship { get; set; } = null!;

    public Guid UserId { get; set; }

    public string TeamName { get; set; } = string.Empty;
    public int? PointsAwarded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
