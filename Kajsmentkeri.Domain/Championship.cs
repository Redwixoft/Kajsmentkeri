namespace Kajsmentkeri.Domain;

public class Championship
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Description { get; set; }

    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public ChampionshipScoringRules ScoringRules { get; set; } = null!;
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
