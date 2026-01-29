namespace Kajsmentkeri.Domain;

public class Championship
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Description { get; set; }
    public bool EnforceLeaderboardVisibilityRules { get; set; }
    public bool SupportsChampionshipWinnerPrediction { get; set; }
    public bool IsChampionshipEnded { get; set; }
    public string? WinnerIban { get; set; }
    public string? WinnerNote { get; set; }

    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public ChampionshipScoringRules ScoringRules { get; set; } = null!;
    public ICollection<Match> Matches { get; set; } = new List<Match>();
    public bool IsTest { get; set; }
}
