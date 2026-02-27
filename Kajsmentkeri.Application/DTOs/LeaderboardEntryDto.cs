namespace Kajsmentkeri.Application.DTOs;

public class LeaderboardEntryDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    public int TotalPoints { get; set; }
    public int CorrectWinners { get; set; }
    public int OneGoalMisses { get; set; }
    public int OnlyCorrect { get; set; }
    public int OnlyOneTries { get; set; }
    public int ExactScores { get; set; }
    public int WinnerPredictionPoints { get; set; }
    public int ChampionshipCount { get; set; }
    public int TotalScoredPredictions { get; set; }

    public decimal RarityPoints { get; set; }
}
