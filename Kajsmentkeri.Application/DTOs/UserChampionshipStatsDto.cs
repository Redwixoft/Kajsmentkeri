namespace Kajsmentkeri.Application.DTOs;

public class UserChampionshipStatsDto
{
    public Guid ChampionshipId { get; set; }
    public string ChampionshipName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int TotalPoints { get; set; }
    public int CorrectWinners { get; set; }
    public int OneGoalMisses { get; set; }
    public int ExactScores { get; set; }
    public int OnlyCorrect { get; set; }
    public int OnlyOneTries { get; set; }
    public int Rank { get; set; }
    public int TotalParticipants { get; set; }
}
