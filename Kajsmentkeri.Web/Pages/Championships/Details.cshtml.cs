using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Application.Models;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Web.Pages.Championships;

public class DetailsModel : PageModel
{
    private readonly IChampionshipService _championshipService;
    private readonly IMatchService _matchService;
    private readonly IPredictionService _predictionService;
    private readonly UserManager<AppUser> _userManager;
    private readonly IPredictionScoringService _scoringService;
    private readonly ILeaderboardService _leaderboardService;
    private readonly ILogger<DetailsModel> _logger;
    private readonly ITimeService _timeService;
    private readonly ISafeLockService _safeLockService;

    public DetailsModel(UserManager<AppUser> userManager,
        IPredictionScoringService scoringService,
        ILeaderboardService leaderboardService,
        IChampionshipService championshipService,
        IMatchService matchService,
        IPredictionService predictionService,
        ILogger<DetailsModel> logger,
        ITimeService timeService,
        ISafeLockService safeLockService)
    {
        _userManager = userManager;
        _scoringService = scoringService;
        _leaderboardService = leaderboardService;
        _championshipService = championshipService;
        _matchService = matchService;
        _predictionService = predictionService;
        _logger = logger;
        _timeService = timeService;
        _safeLockService = safeLockService;
    }

    public Championship? Championship { get; set; }
    public bool IsAdmin { get; set; }
    public Guid? CurrentUserId { get; set; }
    public string? MotivationalText { get; set; }
    public Dictionary<Guid, (int ScoreDiff, int ExactDiff)> UserDiffStats { get; set; } = new();
    public ChampionshipPageStats PageStats { get; set; } = new();

    public class ChampionshipPageStats
    {
        public bool HasData { get; set; }
        public int TotalScoredMatches { get; set; }
        public int MatchesNobodyGuessedWinner { get; set; }
        public int MatchesEveryoneGuessedWinner { get; set; }
        public List<string> MostPredictedTeams { get; set; } = new();
        public int MostPredictedTeamCount { get; set; }
        public List<string> MostCorrectWinnerTeams { get; set; } = new();
        public int MostCorrectWinnerTeamCount { get; set; }
        public List<string> MostExactScoreTeams { get; set; } = new();
        public int MostExactScoreTeamCount { get; set; }
        public string? ChampionshipLucker { get; set; }
        public string? ChampionshipUnderdog { get; set; }
        public List<string> MostFailedOnlyOnesTriers { get; set; } = new();
        public int MostFailedOnlyOnesCount { get; set; }
        public List<(string Score, int Count)> TopResults { get; set; } = new();
        public List<(string Score, int Count)> TopPredictions { get; set; } = new();
        public List<string> MostChaoticMatches { get; set; } = new();
        public int MostChaoticMatchVariety { get; set; }
        public int LongestPositiveStreakLength { get; set; }
        public List<string> LongestPositiveStreakUsers { get; set; } = new();
        public int LongestNegativeStreakLength { get; set; }
        public List<string> LongestNegativeStreakUsers { get; set; } = new();
    }

    public List<Match> Matches { get; set; } = new();
    public List<UserColumn> Users { get; set; } = new();
    public Dictionary<(Guid MatchId, Guid UserId), Prediction> PredictionMap { get; set; } = new();
    public List<LeaderboardEntryDto> Leaderboard { get; set; } = new();
    public LineGraphViewModel Graph { get; set; } = new();
    public Dictionary<Guid, int> UserRanks { get; set; } = new();
    public bool IsVisibilityRuleActive { get; set; }
    public HashSet<Guid> FirstMatchesIds { get; set; } = new();
    public HashSet<Guid> LeaderUserIds { get; set; } = new();
    public HashSet<Guid> TailUserIds { get; set; } = new();

    public bool IsCurrentUserParticipating { get; set; }
    public HashSet<Guid> ParticipantUserIds { get; set; } = new();

    // Winner prediction props
    public ChampionshipWinnerPrediction? MyWinnerPrediction { get; set; }
    public List<string> AllTeams { get; set; } = new();
    public List<ChampionshipWinnerPrediction> AllWinnerPredictions { get; set; } = new();
    public bool HasChampionshipStarted { get; set; }

    // Previous championship extremes
    public Guid? PreviousChampionWinnerId { get; set; }
    public Guid? PreviousChampionLoserId { get; set; }
    public string PreviousChampionshipLabel { get; set; } = string.Empty;

    // Safe lock
    public HashSet<Guid> SafeLockMatchIds { get; set; } = new();
    public Dictionary<Guid, SafeLock> CurrentUserSafeLocks { get; set; } = new();
    public Dictionary<Guid, HashSet<Guid>> SafeLockOwnersByMatch { get; set; } = new();
    public List<LeaderboardEntryDto> EligibleTrackedUsers { get; set; } = new();
    public bool BronzeMedalMatchResultAvailable { get; set; } = true;

    [BindProperty]
    public Guid MatchId { get; set; }

    [BindProperty]
    public string WinnerTeamName { get; set; } = string.Empty;

    [BindProperty]
    public string PredictionInput { get; set; } = string.Empty;

    [BindProperty]
    public bool SkipRandomPrompt { get; set; }

    [BindProperty]
    public string ResultInput { get; set; } = string.Empty;

    [BindProperty]
    public string WinnerIban { get; set; } = string.Empty;

    [BindProperty]
    public string WinnerNote { get; set; } = string.Empty;

    [BindProperty] public Guid SafeLockMatchId { get; set; }
    [BindProperty] public Guid SafeLockTrackedUserId { get; set; }
    [BindProperty] public int SafeLockHomeWinHome { get; set; }
    [BindProperty] public int SafeLockHomeWinAway { get; set; }
    [BindProperty] public int SafeLockAwayWinHome { get; set; }
    [BindProperty] public int SafeLockAwayWinAway { get; set; }
    [BindProperty] public int? SafeLockDrawHome { get; set; }
    [BindProperty] public int? SafeLockDrawAway { get; set; }

    public class UserColumn
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var championshipTask = _championshipService.GetByIdAsync(id);
        var leaderboardTask = _leaderboardService.GetLeaderboardAsync(id);
        var matchesTask = _matchService.GetMatchesByChampionshipAsync(id);

        Championship = await championshipTask;
        if (Championship == null)
            return NotFound();

        var extremesTask = _leaderboardService.GetPreviousChampionshipExtremesAsync(id, Championship.Type);

        var currentUser = await _userManager.GetUserAsync(User);
        IsAdmin = currentUser?.IsAdmin == true;
        CurrentUserId = currentUser?.Id;

        Leaderboard = await leaderboardTask;
        Matches = await matchesTask;
        var predictions = Matches.SelectMany(m => m.Predictions).ToList();

        var participantIds = await _championshipService.GetParticipantUserIdsAsync(id);
        var usersWithPredictions = predictions.Select(p => p.UserId).Distinct().ToHashSet();

        // Merge formal participants with users who already have predictions (backwards compatibility)
        ParticipantUserIds = new HashSet<Guid>(participantIds);
        ParticipantUserIds.UnionWith(usersWithPredictions);

        var relevantUserIds = new HashSet<Guid>(ParticipantUserIds);
        if (CurrentUserId.HasValue)
            relevantUserIds.Add(CurrentUserId.Value);

        var users = await _userManager.Users.Where(u => relevantUserIds.Contains(u.Id)).ToListAsync();

        // Add zero-entry rows for participants not yet on the leaderboard
        var leaderboardUserIds = Leaderboard.Select(e => e.UserId).ToHashSet();
        var userNameLookup = users.ToDictionary(u => u.Id, u => u.UserName ?? "?");
        foreach (var participantId in ParticipantUserIds.Where(id => !leaderboardUserIds.Contains(id)))
        {
            Leaderboard.Add(new LeaderboardEntryDto
            {
                UserId = participantId,
                UserName = userNameLookup.GetValueOrDefault(participantId, "?")
            });
        }

        IsCurrentUserParticipating = CurrentUserId.HasValue && ParticipantUserIds.Contains(CurrentUserId.Value);

        // Sort users: logged-in first, then others alphabetically
        Users = users
            .OrderBy(u => u.Id == CurrentUserId ? 0 : 1)
            .ThenBy(u => u.UserName)
            .Select(u => new UserColumn
            {
                UserId = u.Id,
                UserName = u.UserName ?? "?"
            })
            .ToList();

        // Build prediction map: (MatchId, UserId) => "3:1"
        PredictionMap = predictions.ToDictionary(
            p => (p.MatchId, p.UserId),
            p => p
        );

        // Diff stats: score-margin diff and exact diff per user
        var scoredMatches2 = Matches.Where(m => m.HomeScore.HasValue && m.AwayScore.HasValue).ToList();
        var diffAccum = new Dictionary<Guid, (int ScoreDiff, int ExactDiff)>();
        foreach (var m in scoredMatches2)
        {
            int rH = m.HomeScore!.Value, rA = m.AwayScore!.Value;
            foreach (var kvp in PredictionMap.Where(kv => kv.Key.MatchId == m.Id))
            {
                int pH = kvp.Value.PredictedHome, pA = kvp.Value.PredictedAway;
                int scoreDiff = Math.Abs((rH - rA) - (pH - pA));
                int exactDiff = Math.Abs(rH - pH) + Math.Abs(rA - pA);
                var uid = kvp.Key.UserId;
                if (!diffAccum.TryGetValue(uid, out var existing))
                    diffAccum[uid] = (scoreDiff, exactDiff);
                else
                    diffAccum[uid] = (existing.ScoreDiff + scoreDiff, existing.ExactDiff + exactDiff);
            }
        }
        UserDiffStats = diffAccum;
        PageStats = BuildPageStats();

        // Visibility Rule Logic
        var finishedMatchesCount = Matches.Count(m => m.HomeScore.HasValue && m.AwayScore.HasValue);
        IsVisibilityRuleActive = Championship.EnforceLeaderboardVisibilityRules && finishedMatchesCount >= 8;

        // Competition ranking: tied entries share the same position (1, 1, 3, not 1, 2, 3)
        int compRank = 1;
        for (int i = 0; i < Leaderboard.Count; i++)
        {
            if (i > 0 && !AreLeaderboardEntriesTied(Leaderboard[i], Leaderboard[i - 1]))
                compRank = i + 1;
            UserRanks[Leaderboard[i].UserId] = compRank;
        }

        var userNameMap = users
            .Where(u => ParticipantUserIds.Contains(u.Id))
            .ToDictionary(u => u.Id, u => u.UserName ?? "?");
        var scoredMatches = Matches
            .Where(m => m.HomeScore.HasValue && m.AwayScore.HasValue)
            .OrderBy(m => m.StartTimeUtc)
            .ToList();
        Graph = _leaderboardService.BuildLeaderboardProgress(scoredMatches, predictions, userNameMap);

        var firstMatchUtc = Matches.OrderBy(m => m.StartTimeUtc).FirstOrDefault()?.StartTimeUtc;
        HasChampionshipStarted = firstMatchUtc.HasValue && firstMatchUtc.Value <= _timeService.UtcNow;

        var extremes = await extremesTask;
        if (extremes != null)
        {
            PreviousChampionWinnerId = extremes.Value.WinnerUserId;
            PreviousChampionLoserId = extremes.Value.LoserUserId;
            PreviousChampionshipLabel = extremes.Value.ChampionshipLabel;
        }

        if (Championship.SupportsChampionshipWinnerPrediction)
        {
            AllTeams = Matches
                .SelectMany(m => new[] { m.HomeTeam, m.AwayTeam })
                .GroupBy(t => t)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key)
                .OrderBy(t => t)
                .ToList();
            AllWinnerPredictions = await _predictionService.GetWinnerPredictionsForChampionshipAsync(id);
            if (CurrentUserId.HasValue)
            {
                MyWinnerPrediction = await _predictionService.GetWinnerPredictionAsync(id, CurrentUserId.Value);
            }
        }

        if (CurrentUserId.HasValue && Championship.IsChampionshipEnded == false)
        {
            var myRank = UserRanks.GetValueOrDefault(CurrentUserId.Value, 0);
            if (myRank > 0)
            {
                var myEntry = Leaderboard.FirstOrDefault(e => e.UserId == CurrentUserId.Value);
                var pointGap = myEntry != null && Leaderboard.Count > 1
                    ? Leaderboard[0].TotalPoints - myEntry.TotalPoints
                    : 0;
                MotivationalText = PickMotivationalText(myRank, Leaderboard.Count, pointGap);
            }
        }

        // Safe lock: eligible matches are the final and the bronze medal match
        var bronzeMatch = Matches.FirstOrDefault(m => m.IsBronzeMedalMatch == true);
        BronzeMedalMatchResultAvailable = bronzeMatch == null || (bronzeMatch.HomeScore != null && bronzeMatch.AwayScore != null);

        var finalMatches = Matches.Where(m => m.IsFinalMatch == true).ToList();
        var bronzeMatches = bronzeMatch != null ? new List<Match> { bronzeMatch } : new List<Match>();
        var eligibleMatches = finalMatches.Concat(bronzeMatches).ToList();

        SafeLockMatchIds = eligibleMatches.Count > 0
            ? eligibleMatches.Select(m => m.Id).ToHashSet()
            : (Matches.Count > 0 ? new HashSet<Guid> { Matches.Last().Id } : new HashSet<Guid>());

        if (SafeLockMatchIds.Count > 0)
        {
            SafeLockOwnersByMatch = await _safeLockService.GetOwnerIdsWithSafeLocksByMatchAsync(SafeLockMatchIds);

            if (CurrentUserId.HasValue)
            {
                CurrentUserSafeLocks = await _safeLockService.GetSafeLocksForOwnerAsync(SafeLockMatchIds, CurrentUserId.Value);

                var myRank = UserRanks.GetValueOrDefault(CurrentUserId.Value, int.MaxValue);
                EligibleTrackedUsers = Leaderboard
                    .Where(e => UserRanks.GetValueOrDefault(e.UserId, int.MaxValue) < myRank)
                    .ToList();
            }
        }

        // Pre-calculate data for lock logic to avoid N+1 queries in the view
        FirstMatchesIds = Matches.OrderBy(m => m.StartTimeUtc).Take(8).Select(m => m.Id).ToHashSet();
        if (Leaderboard.Count > 0)
        {
            var leadRank = UserRanks[Leaderboard.First().UserId];
            var tailRank = UserRanks[Leaderboard.Last().UserId];
            LeaderUserIds = Leaderboard.Where(e => UserRanks[e.UserId] == leadRank).Select(e => e.UserId).ToHashSet();
            TailUserIds = Leaderboard.Where(e => UserRanks[e.UserId] == tailRank).Select(e => e.UserId).ToHashSet();
        }

        return Page();
    }

    public bool IsPredictionLockedSync(Guid matchId, Guid userId)
    {
        var match = Matches.FirstOrDefault(m => m.Id == matchId);
        if (match == null) return true;

        var matchStart = match.StartTimeUtc;

        if (FirstMatchesIds.Contains(matchId))
            return matchStart <= _timeService.UtcNow;

        if (LeaderUserIds.Contains(userId))
            return matchStart.AddMinutes(-10) <= _timeService.UtcNow;

        if (TailUserIds.Contains(userId))
            return matchStart.AddMinutes(5) <= _timeService.UtcNow;

        return matchStart <= _timeService.UtcNow;
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var match = await _matchService.GetMatchByIdAsync(MatchId);
        if (match == null)
            return BadRequest("Match not found.");

        // Parse input like "3:1"
        var parts = PredictionInput.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var home) || !int.TryParse(parts[1], out var away))
        {
            ModelState.AddModelError(string.Empty, "Invalid format. Use X:Y");
            return await OnGetAsync(id); // redisplay with error
        }

        await _predictionService.SubmitPredictionAsync(MatchId, home, away);

        if (match.AwayScore != null)
        {
            await _scoringService.RecalculateForMatchAsync(match.Id);
        }

        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostAsync)} (Championship - Predicton POST) end: {_timeService.UtcNow}");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSubmitPredictionAjaxAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return new JsonResult(new { success = false, message = "User not logged in" }) { StatusCode = 401 };

        var match = await _matchService.GetMatchByIdAsync(MatchId);
        if (match == null)
            return new JsonResult(new { success = false, message = "Match not found" }) { StatusCode = 404 };

        // Parse input like "3:1"
        var parts = PredictionInput.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var home) || !int.TryParse(parts[1], out var away))
        {
            return new JsonResult(new { success = false, message = "Invalid format. Use X:Y" });
        }

        var championship = await _championshipService.GetByIdAsync(match.ChampionshipId);
        if (championship?.IsDrawEnabled == false && home == away)
        {
            return new JsonResult(new { success = false, message = "There are no ties in ice hockey you fucking moron!" });
        }

        if (!SkipRandomPrompt && Random.Shared.NextDouble() < 0.05)
        {
            var (randomHome, randomAway) = GenerateRandomPrediction(home, away, championship?.IsDrawEnabled != false);
            var userScore = $"{home}:{away}";
            var randomScore = $"{randomHome}:{randomAway}";
            var message = RoastTemplates[Random.Shared.Next(RoastTemplates.Length)]
                .Replace("USER_SCORE", userScore)
                .Replace("RANDOM_SCORE", randomScore);

            return new JsonResult(new
            {
                success = true,
                requiresConfirmation = true,
                message,
                userScore,
                randomScore
            });
        }

        try
        {
            await _predictionService.SubmitPredictionAsync(MatchId, home, away);
            
            if (match.AwayScore != null)
            {
                await _scoringService.RecalculateForMatchAsync(match.Id);
            }

            return new JsonResult(new { success = true, message = "Saved" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving prediction");
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    private static readonly string[] RoastTemplates =
    {
        "Are you seriously rolling with USER_SCORE? Did you let your toddler pick that? Do yourself a favor and take RANDOM_SCORE instead, you absolute clown.",
        "Wow. USER_SCORE. Bold strategy to announce to the whole group chat that you know absolutely nothing about sports. Just take RANDOM_SCORE and pretend you have a brain.",
        "Are you blind, stupid, or both? USER_SCORE is a historically garbage take. Click here to swap it for RANDOM_SCORE before you embarrass yourself further.",
        "USER_SCORE? Are you fucking high? A blind monkey throwing darts could pick a better score than that. Take RANDOM_SCORE and thank me later.",
        "Look at this dumbass choosing USER_SCORE. It's not too late to delete this shit and use RANDOM_SCORE. Don't say I didn't warn you when you're at the bottom of the leaderboard.",
        "Oh, honey... no. USER_SCORE is adorable, but we live in reality. Let's change that to RANDOM_SCORE so you have an actual prayer of getting points.",
        "I've seen some awful predictions in my life, but your USER_SCORE really takes the cake. Want to swap that garbage for RANDOM_SCORE, or are you committed to losing?",
        "Is USER_SCORE a typo, or are you just genuinely bad at this? Let me pity-upgrade you to RANDOM_SCORE.",
        "Click 'Yes' if you want to lock in your pathetic USER_SCORE. Click 'No' if you want to admit defeat and let me give you a much better RANDOM_SCORE."
    };

    private static (int Home, int Away) GenerateRandomPrediction(int userHome, int userAway, bool allowDraw)
    {
        int home, away;
        do
        {
            home = Random.Shared.Next(0, 5);
            away = Random.Shared.Next(0, 5);
        } while ((home == userHome && away == userAway) || (!allowDraw && home == away));

        return (home, away);
    }

    public async Task<IActionResult> OnPostUpdateResultAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var match = await _matchService.GetMatchByIdAsync(MatchId);
        if (match == null || match.StartTimeUtc > _timeService.UtcNow)
            return BadRequest("Cannot update result before match starts.");

        var parts = ResultInput.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var home) || !int.TryParse(parts[1], out var away))
        {
            ModelState.AddModelError(string.Empty, "Invalid result format. Use X:Y. Omg you are admin user, you should have known that.");
            return await OnGetAsync(id); // Redisplay page with errors
        }

        await _matchService.UpdateMatchResultAsync(MatchId, home, away, user.Id, user.UserName);
        await _scoringService.RecalculateForMatchAsync(MatchId);

        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostUpdateResultAsync)} (Championship - Result POST) start: {_timeService.UtcNow}");
        return RedirectToPage(new { id });
    }


    public async Task<IActionResult> OnGetAuditLogsAsync(Guid matchId)
    {
        var logs = await _predictionService.GetAuditLogsForMatchAsync(matchId);

        // Walk the log chronologically to figure out, for each entry, which other
        // users already held that exact prediction at that point in time.
        var userScores = new Dictionary<Guid, (int Home, int Away)>();
        var userNames = new Dictionary<Guid, string>();
        var othersByLogId = new Dictionary<Guid, List<string>>();
        foreach (var l in logs.OrderBy(l => l.TimestampUtc))
        {
            userNames[l.TargetUserId] = l.TargetUserName;

            if (l.IsRejected || l.IsSafeLockCreated || l.IsSafeLockRemoved || l.IsHighConfidenceSet || l.IsHighConfidenceRemoved || l.IsResultUpdate)
                continue;

            var newScore = (l.NewHomeScore, l.NewAwayScore);
            othersByLogId[l.Id] = userScores
                .Where(kv => kv.Key != l.TargetUserId && kv.Value == newScore)
                .Select(kv => userNames.TryGetValue(kv.Key, out var n) ? n : kv.Key.ToString())
                .ToList();

            userScores[l.TargetUserId] = newScore;
        }

        var formattedLogs = new List<object>();
        foreach (var l in logs)
        {
            var timestamp = _timeService.ToBratislava(l.TimestampUtc).ToString("yyyy-MM-dd HH:mm:ss");
            string message;
            if (l.IsRejected)
            {
                message = $"<span class='text-danger'>🚫 [BLOCKED]</span> <b>{l.AdminName}</b> attempted to submit " +
                          $"<b>{l.NewHomeScore}:{l.NewAwayScore}</b> on match <b>{l.MatchSummary}</b> but the match was already locked.";
            }
            else if (l.IsSafeLockCreated)
            {
                message = $"🔐 [SAFE LOCK SET] <b>{l.AdminName}</b> set a safe lock on <b>{l.MatchSummary}</b> " +
                          $"tracking <b>{l.TargetUserName}</b>.";
            }
            else if (l.IsSafeLockRemoved)
            {
                message = $"🔓 [SAFE LOCK REMOVED] Safe lock by <b>{l.AdminName}</b> on <b>{l.MatchSummary}</b> " +
                          $"(tracking <b>{l.TargetUserName}</b>) was removed.";
            }
            else if (l.IsResultUpdate)
            {
                message = $"⚽ [RESULT] <b>{l.AdminName}</b> {(l.OldHomeScore == null ? "set" : "updated")} the result of " +
                          $"<b>{l.MatchSummary}</b> to <b>{l.NewHomeScore}:{l.NewAwayScore}</b>" +
                          $"{(l.OldHomeScore == null ? "" : $" (was {l.OldHomeScore}:{l.OldAwayScore} before)")}.";
            }
            else if (l.IsHighConfidenceSet)
            {
                message = $"🚀 [HIGH CONFIDENCE] <b>{l.TargetUserName}</b> marked their prediction " +
                          $"<b>{l.NewHomeScore}:{l.NewAwayScore}</b> on <b>{l.MatchSummary}</b> as high confidence.";
            }
            else if (l.IsHighConfidenceRemoved)
            {
                message = $"🚀 [HIGH CONFIDENCE REMOVED] <b>{l.TargetUserName}</b> removed high confidence from their prediction " +
                          $"<b>{l.NewHomeScore}:{l.NewAwayScore}</b> on <b>{l.MatchSummary}</b>.";
            }
            else if (l.IsSafeLockTrigger)
            {
                var outcome = l.NewHomeScore > l.NewAwayScore ? "Home Win"
                    : l.NewHomeScore < l.NewAwayScore ? "Away Win" : "Draw";
                message = $"🔐 [SAFE LOCK] <b>{l.TargetUserName}</b>'s prediction was set to " +
                          $"<b>{l.NewHomeScore}:{l.NewAwayScore}</b> (triggered by <b>{l.AdminName}</b> switching to {outcome})";
            }
            else if (l.IsAdminUpdate)
            {
                message = $"<span class='text-danger'>[ADMIN]</span> <b>{l.AdminName}</b> {(l.OldHomeScore == null ? "added" : "updated")} prediction for user <b>{l.TargetUserName}</b> " +
                          $"on match <b>{l.MatchSummary}</b>. " +
                          $"The prediction is now <b>{l.NewHomeScore}:{l.NewAwayScore}</b> " +
                          $"{(l.OldHomeScore == null ? "" : $"(was {l.OldHomeScore}:{l.OldAwayScore} before)")}.";
            }
            else
            {
                message = $"<b>{l.AdminName}</b> {(l.OldHomeScore == null ? "added" : "updated")} their prediction " +
                          $"on match <b>{l.MatchSummary}</b>. " +
                          $"The prediction is now <b>{l.NewHomeScore}:{l.NewAwayScore}</b> " +
                          $"{(l.OldHomeScore == null ? "" : $"(was {l.OldHomeScore}:{l.OldAwayScore} before)")}.";
            }

            if (othersByLogId.TryGetValue(l.Id, out var others) && others.Count > 0)
                message += $" Same prediction was already added by: {string.Join(", ", others)}.";

            formattedLogs.Add(new { timestamp, message });
        }

        return new JsonResult(formattedLogs);
    }

    public async Task<IActionResult> OnPostJoinChampionshipAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return new JsonResult(new { success = false, message = "User not logged in" }) { StatusCode = 401 };

        try
        {
            await _championshipService.JoinChampionshipAsync(id, user.Id);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostToggleHighConfidenceAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return new JsonResult(new { success = false, message = "User not logged in" }) { StatusCode = 401 };

        try
        {
            await _predictionService.ToggleHighConfidencePredictionAsync(MatchId);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostSubmitWinnerPredictionAsync(Guid id)
    {
        if (string.IsNullOrWhiteSpace(WinnerTeamName))
            return RedirectToPage(new { id });

        try
        {
            await _predictionService.SubmitWinnerPredictionAsync(id, WinnerTeamName);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSetSafeLockAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return new JsonResult(new { success = false, message = "User not logged in" }) { StatusCode = 401 };

        try
        {
            await _safeLockService.SetSafeLockAsync(
                SafeLockMatchId, user.Id, SafeLockTrackedUserId,
                SafeLockHomeWinHome, SafeLockHomeWinAway,
                SafeLockAwayWinHome, SafeLockAwayWinAway,
                SafeLockDrawHome, SafeLockDrawAway);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostRemoveSafeLockAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return new JsonResult(new { success = false, message = "User not logged in" }) { StatusCode = 401 };

        try
        {
            await _safeLockService.RemoveSafeLockAsync(SafeLockMatchId, user.Id);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostEndChampionshipAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.IsAdmin != true)
            return Forbid();

        try
        {
            await _championshipService.EndChampionshipAsync(id);
            TempData["SuccessMessage"] = "Championship ended and winner points awarded!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRecalculateChampionshipAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.IsAdmin != true)
            return Forbid();

        try
        {
            await _championshipService.RecalculateChampionshipAsync(id);
            TempData["SuccessMessage"] = "Championship recalculated and winner points updated!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    private ChampionshipPageStats BuildPageStats()
    {
        var scoredMatches = Matches.Where(m => m.HomeScore.HasValue && m.AwayScore.HasValue).ToList();
        var result = new ChampionshipPageStats { TotalScoredMatches = scoredMatches.Count };
        if (scoredMatches.Count == 0 || PredictionMap.Count == 0) return result;
        result.HasData = true;

        // Matches where nobody / everyone guessed the winner
        foreach (var m in scoredMatches)
        {
            var preds = PredictionMap
                .Where(kv => kv.Key.MatchId == m.Id && ParticipantUserIds.Contains(kv.Key.UserId))
                .Select(kv => kv.Value).ToList();
            if (preds.Count == 0) continue;
            var correct = preds.Count(p => p.GotWinner);
            if (correct == 0) result.MatchesNobodyGuessedWinner++;
            if (correct == preds.Count) result.MatchesEveryoneGuessedWinner++;
        }

        // Most predicted team (winner) + most correctly guessed team (winner + exact score)
        var teamPredCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var teamWinnerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var teamExactCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in scoredMatches)
        {
            var preds = PredictionMap.Where(kv => kv.Key.MatchId == m.Id).Select(kv => kv.Value).ToList();
            foreach (var p in preds)
            {
                // Team the predictor picked to win (draws count for neither team)
                string? predictedWinner =
                    p.PredictedHome > p.PredictedAway ? m.HomeTeam :
                    p.PredictedHome < p.PredictedAway ? m.AwayTeam :
                    null;
                if (predictedWinner != null)
                {
                    teamPredCounts.TryGetValue(predictedWinner, out var t);
                    teamPredCounts[predictedWinner] = t + 1;
                }
            }
            var winnerCount = preds.Count(p => p.GotWinner);
            var exactCount = preds.Count(p => p.GotExactScore);
            foreach (var team in new[] { m.HomeTeam, m.AwayTeam })
            {
                teamWinnerCounts.TryGetValue(team, out var w);
                teamWinnerCounts[team] = w + winnerCount;
                teamExactCounts.TryGetValue(team, out var e);
                teamExactCounts[team] = e + exactCount;
            }
        }
        if (teamPredCounts.Count > 0)
        {
            result.MostPredictedTeamCount = teamPredCounts.Values.Max();
            result.MostPredictedTeams = teamPredCounts
                .Where(kv => kv.Value == result.MostPredictedTeamCount)
                .Select(kv => kv.Key).OrderBy(t => t).ToList();
        }
        if (teamWinnerCounts.Count > 0)
        {
            result.MostCorrectWinnerTeamCount = teamWinnerCounts.Values.Max();
            result.MostCorrectWinnerTeams = teamWinnerCounts
                .Where(kv => kv.Value == result.MostCorrectWinnerTeamCount)
                .Select(kv => kv.Key).OrderBy(t => t).ToList();
        }
        if (teamExactCounts.Count > 0)
        {
            result.MostExactScoreTeamCount = teamExactCounts.Values.Max();
            result.MostExactScoreTeams = teamExactCounts
                .Where(kv => kv.Value == result.MostExactScoreTeamCount)
                .Select(kv => kv.Key).OrderBy(t => t).ToList();
        }

        // Championship Lucker — lowest one-goal miss / exact score ratio (must have at least one exact score).
        // Ties broken by most exact scores, then name, for deterministic results.
        var lucker = Leaderboard
            .Where(e => e.ExactScores > 0)
            .OrderBy(e => (double)e.OneGoalMisses / e.ExactScores)
            .ThenByDescending(e => e.ExactScores)
            .ThenBy(e => e.UserName)
            .FirstOrDefault();
        if (lucker != null)
            result.ChampionshipLucker = lucker.UserName;

        // Championship Underdog — highest one-goal miss / exact score ratio (0 exact scores = worst).
        // Ties broken by most one-goal misses, then name, for deterministic results.
        var underdog = Leaderboard
            .Where(e => e.OneGoalMisses > 0)
            .OrderByDescending(e => (double)e.OneGoalMisses / (e.ExactScores + 0.5))
            .ThenByDescending(e => e.OneGoalMisses)
            .ThenBy(e => e.UserName)
            .FirstOrDefault();
        if (underdog != null)
            result.ChampionshipUnderdog = underdog.UserName;

        // Most failed only-one tries — all users tied at the top are shown
        var maxFailedOnlyOnes = Leaderboard.Count > 0
            ? Leaderboard.Max(e => e.OnlyOneTries - e.OnlyCorrect)
            : 0;
        if (maxFailedOnlyOnes > 0)
        {
            result.MostFailedOnlyOnesCount = maxFailedOnlyOnes;
            result.MostFailedOnlyOnesTriers = Leaderboard
                .Where(e => e.OnlyOneTries - e.OnlyCorrect == maxFailedOnlyOnes)
                .Select(e => e.UserName)
                .OrderBy(u => u)
                .ToList();
        }

        // Most common results — all tied at top count (normalized, per unique match)
        var resultGroups = scoredMatches
            .GroupBy(m => m.HomeScore!.Value >= m.AwayScore!.Value
                ? (m.HomeScore.Value, m.AwayScore.Value)
                : (m.AwayScore.Value, m.HomeScore.Value))
            .Select(g => ($"{g.Key.Item1}:{g.Key.Item2}", g.Count()))
            .OrderByDescending(x => x.Item2)
            .ToList();
        if (resultGroups.Count > 0)
        {
            var topCount = resultGroups[0].Item2;
            result.TopResults = resultGroups.Where(x => x.Item2 == topCount).ToList();
        }

        // Most common predictions — all tied at top count (normalized, across all predictions)
        var predGroups = PredictionMap.Values
            .GroupBy(p => p.PredictedHome >= p.PredictedAway
                ? (p.PredictedHome, p.PredictedAway)
                : (p.PredictedAway, p.PredictedHome))
            .Select(g => ($"{g.Key.Item1}:{g.Key.Item2}", g.Count()))
            .OrderByDescending(x => x.Item2)
            .ToList();
        if (predGroups.Count > 0)
        {
            var topCount = predGroups[0].Item2;
            result.TopPredictions = predGroups.Where(x => x.Item2 == topCount).ToList();
        }

        // Most chaotic match — all matches tied at the highest unique predicted score variety
        var matchVarieties = scoredMatches
            .Select(m =>
            {
                var preds = PredictionMap.Where(kv => kv.Key.MatchId == m.Id).Select(kv => kv.Value).ToList();
                var variety = preds.Count == 0 ? 0 : preds
                    .Select(p => p.PredictedHome >= p.PredictedAway
                        ? (p.PredictedHome, p.PredictedAway)
                        : (p.PredictedAway, p.PredictedHome))
                    .Distinct().Count();
                return (Label: $"{m.HomeTeam} - {m.AwayTeam}", Variety: variety);
            })
            .Where(x => x.Variety > 0)
            .ToList();
        if (matchVarieties.Count > 0)
        {
            result.MostChaoticMatchVariety = matchVarieties.Max(x => x.Variety);
            result.MostChaoticMatches = matchVarieties
                .Where(x => x.Variety == result.MostChaoticMatchVariety)
                .Select(x => x.Label).ToList();
        }

        // Longest positive / negative streaks across participants
        var sortedScoredMatches = scoredMatches.OrderBy(m => m.StartTimeUtc).ToList();
        var userNameLookup = Users.ToDictionary(u => u.UserId, u => u.UserName);
        var bestPosStreak = 0;
        var bestPosUsers = new List<string>();
        var bestNegStreak = 0;
        var bestNegUsers = new List<string>();

        foreach (var userId in ParticipantUserIds.Where(id => userNameLookup.ContainsKey(id)))
        {
            int posStreak = 0, negStreak = 0, maxPos = 0, maxNeg = 0;
            foreach (var m in sortedScoredMatches)
            {
                var gotPoints = PredictionMap.TryGetValue((m.Id, userId), out var pred) && pred.Points > 0;
                if (gotPoints) { posStreak++; negStreak = 0; }
                else           { negStreak++; posStreak = 0; }
                if (posStreak > maxPos) maxPos = posStreak;
                if (negStreak > maxNeg) maxNeg = negStreak;
            }

            var name = userNameLookup[userId];
            if      (maxPos > bestPosStreak) { bestPosStreak = maxPos; bestPosUsers = [name]; }
            else if (maxPos == bestPosStreak && maxPos > 0) bestPosUsers.Add(name);

            if      (maxNeg > bestNegStreak) { bestNegStreak = maxNeg; bestNegUsers = [name]; }
            else if (maxNeg == bestNegStreak && maxNeg > 0) bestNegUsers.Add(name);
        }

        if (bestPosStreak > 1)
        {
            result.LongestPositiveStreakLength = bestPosStreak;
            result.LongestPositiveStreakUsers = bestPosUsers.OrderBy(u => u).ToList();
        }
        if (bestNegStreak > 1)
        {
            result.LongestNegativeStreakLength = bestNegStreak;
            result.LongestNegativeStreakUsers = bestNegUsers.OrderBy(u => u).ToList();
        }

        return result;
    }

    private static bool AreLeaderboardEntriesTied(LeaderboardEntryDto a, LeaderboardEntryDto b) =>
        a.TotalPoints == b.TotalPoints &&
        a.CorrectWinners == b.CorrectWinners &&
        a.OneGoalMisses == b.OneGoalMisses &&
        a.OnlyCorrect == b.OnlyCorrect &&
        a.RarityPoints == b.RarityPoints;

    private static string PickMotivationalText(int rank, int total, int pointGap)
    {
        var rng = new Random();

        if (rank == 1)
        {
            var texts = new[]
            {
                "You are currently leading. Enjoy it while it lasts, we all know it's just a statistical anomaly.",
                "First place. Your sheer luck is genuinely offensive to everyone who actually understands hockey.",
                "Wow, look at you at the top. I bet you think this makes up for your lack of personality.",
                "Congratulations, you're the king of a hill made entirely of garbage.",
                "First place. It's amazing what a man can achieve when he clearly has no social life, no hobbies, and zero responsibilities.",
                "Enjoy the view from the top. Just remember, the higher you climb, the funnier it's going to be when your entire season collapses next week.",
                "Congratulations on being the smartest guy in a room full of absolute idiots. Truly a legendary achievement.",
                "You're leading, but we all know your strategy is just copying random tips and praying. Your fraudulence will be exposed soon.",
                "First place. Wow. I didn't know they handed out trophies for being a virgin with too much free time.",
                "Enjoy the view from the top, asshole. We all know your girlfriend is only staying with you because she thinks your hockey luck translates to the bedroom. Spoiler: it doesn't.",
                "Look at you, leading the pack. It's amazing what you can achieve when you have absolutely no social life, no friends who actually like you, and zero prospects in reality.",
                "You're in first place, but you still look like the kind of guy who smells his own fingers after scratching his ass. A lucky fraud.",
            };
            return texts[rng.Next(texts.Length)];
        }

        if (rank == total)
        {
            var texts = new[]
            {
                "You are in last place. Wow. Congratulations you piece of shit.",
                "Statistically speaking, a coin flip would have yielded better predictions than whatever it is you are doing.",
                "Please stop betting on sports. For your own financial safety, and for our collective sanity.",
                "You are so deep in last place you'd need a telescope just to see the guy who is second worst.",
                "Last place. A trained monkey throwing darts at a board would have a higher success rate than whatever your 'strategy' is.",
                "You're so far behind that your points look like a typo. We are genuinely considering starting a GoFundMe for your hockey knowledge.",
                "Congratulations, you've turned sports betting into a form of performance art. A tragic, embarrassing piece of art.",
                "You are the anchor dragging this entire group chat down into the abyss. Just delete the app and pretend you never liked hockey.",
                "Last place. Honestly, your mother is so fat she has her own gravitational pull, and yet somehow your hockey scores are still the biggest disaster in your family.",
                "You are a complete and utter waste of oxygen. A toddler blindly slapping a touchscreen would have a better success rate than your tragic excuses for 'tips'.",
                "Congratulations, you've achieved absolute rock bottom. If I wanted to kill myself, I'd climb up your ego and jump down to your hockey knowledge.",
                "You are so deep in last place that the guys at the top need a fucking microscope to see your microscopic, pathetic excuse of a score. Just delete yourself from the group.",
            };
            return texts[rng.Next(texts.Length)];
        }

        if (rank == 2 && total > 2)
        {
            var texts = new[]
            {
                $"You are {pointGap} point{(pointGap != 1 ? "s" : "")} behind the first place. Don't even try.",
                "Second place. The first among the losers. Truly, an inspiration to mediocre people everywhere.",
                "So close, yet so far. Just close enough to make the inevitable choke hurt that much more.",
                "You're breathing down the leader's neck. Mostly because you don't know what personal space or decent predictions look like.",
                "Second place. Close enough to taste the victory, but still fundamentally a loser. Enjoy the silver medal, you eternal bridesmaid.",
                "You're chasing the leader like a desperate puppy. Sit down, be humble, and accept that you're just a background character this year.",
                "You're only a few points away from glory, which just means your inevitable, catastrophic choke is going to hurt ten times worse.",
                "Nice try, but nobody remembers who came in second. You are literally just a footnote in the leader's success story.",
                "Second place. The silver medal of disappointment. Your mother must be so used to you almost achieving something before completely disappointing her.",
                "You're chasing the leader like a desperate, pathetic ex. Sit down, shut up, and accept that you are genetically engineered to be a loser.",
                "So close to the top, yet still fundamentally a failure. If your choking hazard was any higher, they'd have to put a warning label on your forehead.",
                "Second place just means you worked twice as hard as the guys at the bottom just to get your hopes brutally crushed at the finish line. Pathetic.",
            };
            return texts[rng.Next(texts.Length)];
        }

        if (rank == total - 1 && total > 3)
        {
            var texts = new[]
            {
                "You're in second-to-last place. The only thing keeping you from absolute rock bottom is the absolute disaster below you.",
                "Look on the bright side: you are failing, but someone else is failing historically. Be grateful for their sacrifice.",
                "Second worst. You can't even do 'being incompetent' right.",
                "One spot away from total humiliation. You can feel the heat from the dumpster fire right behind you.",
                "You are currently in second-to-last place. You should literally be paying rent to the guy below you for protecting your dignity.",
                "You're holding onto the edge of the cliff by your fingernails. One bad game and you're plunging straight into the garbage disposal.",
                "Second worst. You can't even fail properly. If you're going to lose, at least have the decency to commit to the bit and take the bottom spot.",
                "You look up at the middle of the table like it's the Himalayas. Don't look down, the absolute bottom is staring right back into your soul.",
                "Second-to-last. You're like the guy who is so ugly he makes the absolute ugliest guy look slightly better by comparison.",
                "You are literally one bad game away from being the official laughingstock of the entire group. Sleep with one eye open, you absolute failure.",
                "The only reason you aren't in last place is because the guy below you must have suffered a severe traumatic brain injury before the season started.",
                "You're clinging to the penultimate spot like a cockroach surviving a nuclear blast. Nobody wants you here, and you're destined for the trash anyway.",
            };
            return texts[rng.Next(texts.Length)];
        }

        // Middle of the table
        var middleTexts = new[]
        {
            "Ah, the middle of the table. The safe space for people who are too cowardly to risk anything, but too stupid to actually win.",
            "You are the human equivalent of a participation trophy. Completely irrelevant to the entire competition.",
            "Not winning, not losing. Just floating around in the gray abyss of pure, unadulterated mediocrity. Fascinatingly boring.",
            "Your predictions are so aggressively average that the spreadsheet is actively depressed just trying to calculate your existence.",
            "Stuck in the middle. You're like the background noise in a hockey stadium—present, but nobody actually listens to you.",
            "You're just there to inflate the prize pool, aren't you? Thanks for the donation, your sacrifice is noted.",
            "Neither a threat to the top nor a comedy show at the bottom. You are just wasting everyone's bandwidth.",
            "If indifference was a betting strategy, you would be the undisputed world champion.",
            "You've achieved peak stability: perfectly balanced between 'not good enough' and 'not quite a total disaster'.",
            "Your spot on the leaderboard is like a lukewarm soup. Nobody wants it, but it's there anyway.",
            "You're just filling space at this point. A glorified placeholder. If we deleted you from the table, nobody would notice for three weeks.",
            "You're too high up to get mocked properly, and too low to get any respect. You live in the wasteland of the forgotten.",
            "Comfortably in the middle. Safe. Gray. Forgettable. The human equivalent of lukewarm tap water.",
            "You are currently in the middle of the pack. Not good enough to be respected, not bad enough to be funny. Just tragic.",
            "Your predictions are so aggressively average that the algorithm is falling asleep looking at them.",
            "Perfectly balanced in the center. Neither winning nor losing. Just existing, like a true NPC.",
            "The middle of the table. The human equivalent of a beige wall. If you disappeared tomorrow, the only thing we'd miss is your share of the beer money.",
            "Your predictions are so boring and safe that your wife probably fakes her orgasms just to match your level of forced enthusiasm.",
            "You're just a background extra in everyone else's story. A waste of server space. A literal nobody.",
            "Neither winning nor losing—just stuck in the gray zone of absolute impotence. Like a broken microwave, you just spin around and accomplish nothing.",
            "You're the type of guy who goes to a steakhouse and orders a tap water and a side of plain white rice. Unbelievably basic.",
            "Stuck in the middle. Your father definitely wanted a daughter, and looking at your leaderboard position, I can finally see why.",
            "You're not even bad enough to be funny. You're just a sad, forgettable middle-management mistake of a human being.",
            "If apathy had a face, it would look exactly like your pathetic, mid-tier score on this table.",
            "You are the human manifestation of a wet cardboard box. Flaccid, useless, and just waiting to be thrown out with the trash.",
            "You're only in the middle because the guys below you are braindead and the guys above you actually have a pulse. You're just a floating corpse.",
            "Your leaderboard presence is like a fart in an elevator—unwanted, embarrassing, and everyone is just waiting for it to dissipate.",
            "Look at you, hiding in the crowd. Too cowardly to bet big and win, too scared to fail spectacularly. A textbook definition of a beta.",
        };
        return middleTexts[rng.Next(middleTexts.Length)];
    }

    public async Task<IActionResult> OnPostSubmitWinnerPaymentInfoAsync(Guid id)
    {
        try
        {
            await _championshipService.UpdateWinnerPaymentInfoAsync(id, WinnerIban, WinnerNote);
            TempData["SuccessMessage"] = "Payment information updated successfully!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }
}
