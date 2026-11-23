namespace Kajsmentkeri.Domain;

/// <summary>
/// A user's guess (one year later) of what they answered in their original <see cref="PercentagePrediction"/>.
/// One row per user.
/// </summary>
public class PercentagePredictionRecall
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public int Question1 { get; set; }
    public int Question2 { get; set; }
    public int Question3 { get; set; }
    public int Question4 { get; set; }
    public int Question5 { get; set; }
    public int Question6 { get; set; }
    public int Question7 { get; set; }
    public int Question8 { get; set; }
    public int Question9 { get; set; }
    public int Question10 { get; set; }
}
