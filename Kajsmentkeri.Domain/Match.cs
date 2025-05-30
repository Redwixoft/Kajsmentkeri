namespace Kajsmentkeri.Domain;

public class Match
{
    public Guid Id { get; set; }

    public Guid ChampionshipId { get; set; }
    public Championship Championship { get; set; } = null!;

    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;

    public DateTime StartTimeUtc { get; set; }

    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
}
