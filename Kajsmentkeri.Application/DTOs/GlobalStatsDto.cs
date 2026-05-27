namespace Kajsmentkeri.Application.DTOs;

public class GlobalStatsDto
{
    public List<(string Score, int Count)> TopResults { get; set; } = new();
    public int TotalMatchesWithResults { get; set; }
    public List<(string Score, int Count)> TopLuckers { get; set; } = new();
    public int TotalExactScorePredictions { get; set; }
    public List<(string Score, int Count, string? TopUser, int TopUserCount)> TopPredictions { get; set; } = new();
    public List<(string Score, int Count, List<string> Users)> LeastCommonPredictions { get; set; } = new();
}
