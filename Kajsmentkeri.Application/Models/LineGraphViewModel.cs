using Kajsmentkeri.Application.DTOs;

namespace Kajsmentkeri.Application.Models;

public class LineGraphViewModel
{
    public List<string> Labels { get; set; } = new(); // E.g. ["CAN-USA", "SWE-CZE"]
    public List<LineSeriesDto> Series { get; set; } = new();
}
