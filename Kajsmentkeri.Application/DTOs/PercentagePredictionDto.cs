using System.ComponentModel.DataAnnotations;

namespace Kajsmentkeri.Application.DTOs;

public class PercentagePredictionDto
{
    [Range(0, 100)]
    public int Question1 { get; set; }
    [Range(0, 100)]
    public int Question2 { get; set; }
    [Range(0, 100)]
    public int Question3 { get; set; }
    [Range(0, 100)]
    public int Question4 { get; set; }
    [Range(0, 100)]
    public int Question5 { get; set; }
    [Range(0, 100)]
    public int Question6 { get; set; }
    [Range(0, 100)]
    public int Question7 { get; set; }
    [Range(0, 100)]
    public int Question8 { get; set; }
    [Range(0, 100)]
    public int Question9 { get; set; }
    [Range(0, 100)]
    public int Question10 { get; set; }
}
