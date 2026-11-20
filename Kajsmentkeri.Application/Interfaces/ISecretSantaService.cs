namespace Kajsmentkeri.Application.Interfaces;

public interface ISecretSantaService
{
    /// <summary>Rules already assigned to the user (empty if none yet).</summary>
    Task<IReadOnlyList<string>> GetUserRulesAsync(Guid userId);

    /// <summary>
    /// Assigns rules to the user from the unassigned pool. Idempotent: if the user
    /// already has rules, those are returned. Throws InvalidOperationException when
    /// not enough unassigned rules remain.
    /// </summary>
    Task<IReadOnlyList<string>> AssignRulesAsync(Guid userId, string nickname);
}
