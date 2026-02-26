namespace Kajsmentkeri.Domain;

public class ChampionshipParticipation
{
    public Guid Id { get; set; }
    public Guid ChampionshipId { get; set; }
    public Championship Championship { get; set; } = null!;
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; }
}
