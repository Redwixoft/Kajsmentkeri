namespace Kajsmentkeri.Application.DTOs;

public class LineSeriesDto
{
    public string Name { get; set; } = "";      // Player's name
    public List<int> Data { get; set; } = new(); // Cumulative points across matches
}
