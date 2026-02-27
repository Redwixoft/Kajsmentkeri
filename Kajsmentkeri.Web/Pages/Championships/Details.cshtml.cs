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

    public DetailsModel(UserManager<AppUser> userManager, 
        IPredictionScoringService scoringService, 
        ILeaderboardService leaderboardService, 
        IChampionshipService championshipService, 
        IMatchService matchService, 
        IPredictionService predictionService, 
        ILogger<DetailsModel> logger,
        ITimeService timeService)
    {
        _userManager = userManager;
        _scoringService = scoringService;
        _leaderboardService = leaderboardService;
        _championshipService = championshipService;
        _matchService = matchService;
        _predictionService = predictionService;
        _logger = logger;
        _timeService = timeService;
    }

    public Championship? Championship { get; set; }
    public bool IsAdmin { get; set; }
    public Guid? CurrentUserId { get; set; }

    public List<Match> Matches { get; set; } = new();
    public List<UserColumn> Users { get; set; } = new();
    public Dictionary<(Guid MatchId, Guid UserId), Prediction> PredictionMap { get; set; } = new();
    public List<LeaderboardEntryDto> Leaderboard { get; set; } = new();
    public LineGraphViewModel Graph { get; set; } = new();
    public Dictionary<Guid, int> UserRanks { get; set; } = new();
    public bool IsVisibilityRuleActive { get; set; }
    public HashSet<Guid> FirstMatchesIds { get; set; } = new();
    public Guid? LeaderUserId { get; set; }
    public Guid? TailUserId { get; set; }

    public bool IsCurrentUserParticipating { get; set; }
    public HashSet<Guid> ParticipantUserIds { get; set; } = new();

    // Winner prediction props
    public ChampionshipWinnerPrediction? MyWinnerPrediction { get; set; }
    public List<string> AllTeams { get; set; } = new();
    public List<ChampionshipWinnerPrediction> AllWinnerPredictions { get; set; } = new();

    [BindProperty]
    public Guid MatchId { get; set; }

    [BindProperty]
    public string WinnerTeamName { get; set; } = string.Empty;

    [BindProperty]
    public string PredictionInput { get; set; } = string.Empty;

    [BindProperty]
    public string ResultInput { get; set; } = string.Empty;

    [BindProperty]
    public string WinnerIban { get; set; } = string.Empty;

    [BindProperty]
    public string WinnerNote { get; set; } = string.Empty;

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
        var predictionsTask = _predictionService.GetPredictionsForChampionshipAsync(id);

        Championship = await championshipTask;
        if (Championship == null)
            return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        IsAdmin = currentUser?.IsAdmin == true;
        CurrentUserId = currentUser?.Id;

        Leaderboard = await leaderboardTask;
        Matches = await matchesTask;
        var predictions = await predictionsTask;
        var users = await _userManager.Users.ToListAsync();

        var participantIds = await _championshipService.GetParticipantUserIdsAsync(id);
        var usersWithPredictions = predictions.Select(p => p.UserId).Distinct().ToHashSet();

        // Merge formal participants with users who already have predictions (backwards compatibility)
        ParticipantUserIds = new HashSet<Guid>(participantIds);
        ParticipantUserIds.UnionWith(usersWithPredictions);

        IsCurrentUserParticipating = CurrentUserId.HasValue && ParticipantUserIds.Contains(CurrentUserId.Value);

        // Sort users: logged-in first, then others alphabetically
        Users = users
            .Where(u => u.Id == CurrentUserId || ParticipantUserIds.Contains(u.Id))
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

        // Visibility Rule Logic
        var finishedMatchesCount = Matches.Count(m => m.HomeScore.HasValue && m.AwayScore.HasValue);
        IsVisibilityRuleActive = Championship.EnforceLeaderboardVisibilityRules && finishedMatchesCount >= 8;

        for (int i = 0; i < Leaderboard.Count; i++)
        {
            UserRanks[Leaderboard[i].UserId] = i + 1;
        }

        var userNameMap = users
            .Where(u => ParticipantUserIds.Contains(u.Id))
            .ToDictionary(u => u.Id, u => u.UserName ?? "?");
        var scoredMatches = Matches
            .Where(m => m.HomeScore.HasValue && m.AwayScore.HasValue)
            .OrderBy(m => m.StartTimeUtc)
            .ToList();
        Graph = _leaderboardService.BuildLeaderboardProgress(scoredMatches, predictions, userNameMap);

        if (Championship.SupportsChampionshipWinnerPrediction)
        {
            AllTeams = Matches.SelectMany(m => new[] { m.HomeTeam, m.AwayTeam }).Distinct().OrderBy(t => t).ToList();
            AllWinnerPredictions = await _predictionService.GetWinnerPredictionsForChampionshipAsync(id);
            if (CurrentUserId.HasValue)
            {
                MyWinnerPrediction = await _predictionService.GetWinnerPredictionAsync(id, CurrentUserId.Value);
            }
        }

        // Pre-calculate data for lock logic to avoid N+1 queries in the view
        FirstMatchesIds = Matches.OrderBy(m => m.StartTimeUtc).Take(8).Select(m => m.Id).ToHashSet();
        if (Leaderboard.Count > 0)
        {
            LeaderUserId = Leaderboard.First().UserId;
            TailUserId = Leaderboard.Last().UserId;
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

        if (userId == LeaderUserId)
            return matchStart.AddMinutes(-10) <= _timeService.UtcNow;

        if (userId == TailUserId)
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

        await _matchService.UpdateMatchResultAsync(MatchId, home, away);
        await _scoringService.RecalculateForMatchAsync(MatchId);

        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostUpdateResultAsync)} (Championship - Result POST) start: {_timeService.UtcNow}");
        return RedirectToPage(new { id });
    }


    public async Task<IActionResult> OnGetAuditLogsAsync(Guid matchId)
    {
        var logs = await _predictionService.GetAuditLogsForMatchAsync(matchId);
        var formattedLogs = logs.Select(l =>
        {
            var timestamp = _timeService.ToBratislava(l.TimestampUtc).ToString("yyyy-MM-dd HH:mm:ss");
            string message;
            if (l.IsAdminUpdate)
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
                          $"{(l.OldHomeScore == null ? "" : $"(was {l.OldHomeScore}:{l.OldAwayScore} before)")}.";            }
            return new { timestamp, message };
        }).ToList();

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
