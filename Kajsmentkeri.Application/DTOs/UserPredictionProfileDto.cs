namespace Kajsmentkeri.Application.DTOs;

public class UserPredictionProfileDto
{
    public string? MostCommonPrediction { get; set; }
    public int MostCommonPredictionCount { get; set; }
    public string? BestTeam { get; set; }
    public int BestTeamCount { get; set; }
    public string? WorstTeam { get; set; }
    public int WorstTeamCount { get; set; }
}
