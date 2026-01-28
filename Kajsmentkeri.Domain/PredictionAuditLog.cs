using System;

namespace Kajsmentkeri.Domain;

public class PredictionAuditLog
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid AdminId { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public Guid TargetUserId { get; set; }
    public string TargetUserName { get; set; } = string.Empty;
    public int? OldHomeScore { get; set; }
    public int? OldAwayScore { get; set; }
    public int NewHomeScore { get; set; }
    public int NewAwayScore { get; set; }
    public DateTime TimestampUtc { get; set; }
    public bool IsAdminUpdate { get; set; }
    public string MatchSummary { get; set; } = string.Empty; // e.g. "Canada - France"
}
