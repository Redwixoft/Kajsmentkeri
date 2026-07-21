using Kajsmentkeri.Domain;

namespace Kajsmentkeri.Application.Interfaces;

public interface ISafeLockService
{
    Task<SafeLock?> GetSafeLockAsync(Guid matchId, Guid ownerUserId);
    Task<Dictionary<Guid, SafeLock>> GetSafeLocksForOwnerAsync(IEnumerable<Guid> matchIds, Guid ownerUserId);
    Task<Dictionary<Guid, HashSet<Guid>>> GetOwnerIdsWithSafeLocksByMatchAsync(IEnumerable<Guid> matchIds);
    Task SetSafeLockAsync(Guid matchId, Guid ownerUserId, Guid trackedUserId,
        int homeWinHome, int homeWinAway, int awayWinHome, int awayWinAway,
        int? drawHome, int? drawAway);
    Task RemoveSafeLockAsync(Guid matchId, Guid ownerUserId);
    Task EvaluateSafeLocksAsync(Guid matchId, Guid changedUserId,
        int? oldHome, int? oldAway, int newHome, int newAway);
    Task ValidateSafeLocksAfterRescoreAsync(Guid championshipId);
}
