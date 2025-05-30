namespace Kajsmentkeri.Domain;

public class ChampionshipScoringRules
{
    public Guid Id { get; set; }

    public Guid ChampionshipId { get; set; }
    public Championship Championship { get; set; } = null!;

    public int PointsForCorrectWinner { get; set; } = 3;
    public int PointsForExactScore { get; set; } = 2;
    public int PointsForOnlyCorrectWinner { get; set; } = 2;
    public int RarityPointsBonus { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
