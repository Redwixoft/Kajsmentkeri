namespace Kajsmentkeri.Domain;

public class SecretSantaRuleAssignment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserNickname { get; set; } = string.Empty;
    public string RuleText { get; set; } = string.Empty;
    public DateTime AssignedAtUtc { get; set; }
}
