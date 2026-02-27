namespace Kajsmentkeri.Application.DTOs;

public class ChampionshipRecordsDto
{
    public List<RecordEntryDto> MostPoints { get; set; } = new();
    public List<RecordEntryDto> MostWinners { get; set; } = new();
    public List<RecordEntryDto> MostOneGoalMisses { get; set; } = new();
    public List<RecordEntryDto> MostLuckers { get; set; } = new();
    public List<RecordEntryDto> MostOnlyOnes { get; set; } = new();
    public List<RecordEntryDto> HighestPointGap { get; set; } = new();
    public List<RecordEntryDto> LongestPositiveStreak { get; set; } = new();
    public List<RecordEntryDto> LongestNegativeStreak { get; set; } = new();
}
