using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Application.Models;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

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

    public DetailsModel(UserManager<AppUser> userManager, IPredictionScoringService scoringService, ILeaderboardService leaderboardService, IChampionshipService championshipService, IMatchService matchService, IPredictionService predictionService, ILogger<DetailsModel> logger)
    {
        _userManager = userManager;
        _scoringService = scoringService;
        _leaderboardService = leaderboardService;
        _championshipService = championshipService;
        _matchService = matchService;
        _predictionService = predictionService;
        _logger = logger;
    }

    public Championship? Championship { get; set; }
    public bool IsAdmin { get; set; }
    public Guid? CurrentUserId { get; set; }

    public List<Match> Matches { get; set; } = new();
    public List<UserColumn> Users { get; set; } = new();
    public Dictionary<(Guid MatchId, Guid UserId), Prediction> PredictionMap { get; set; } = new();
    public List<LeaderboardEntryDto> Leaderboard { get; set; } = new();
    public LineGraphViewModel Graph { get; set; } = new();

    public List<string> Logs { get; set; } = new List<string>();

    [BindProperty]
    public Guid MatchId { get; set; }

    [BindProperty]
    public string PredictionInput { get; set; } = string.Empty;

    [BindProperty]
    public string ResultInput { get; set; } = string.Empty;

    public class UserColumn
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(Guid id, List<string>? logs = null)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        if (logs != null)
        {
            Logs.AddRange(logs);
        }
        Logs.Add($"OnGetAsync start, total elapsed: {stopwatch.ElapsedMilliseconds}");

        var championshipTask = _championshipService.GetByIdAsync(id);
        var leaderboardTask = _leaderboardService.GetLeaderboardAsync(id);
        var matchesTask = _matchService.GetMatchesByChampionshipAsync(id);
        var predictionsTask = _predictionService.GetPredictionsForChampionshipAsync(id);
        var leaderboardProgressTask = _leaderboardService.GetLeaderboardProgressAsync(id);

       // await Task.WhenAll(championshipTask, leaderboardTask, matchesTask, predictionsTask, leaderboardProgressTask);

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

        Graph = await leaderboardProgressTask; 

        stopwatch.Stop();
        Logs.Add($"OnGetAsync end, total elapsed: {stopwatch.ElapsedMilliseconds}");
        stopwatch.Restart();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
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

        var logs = new List<string>() { $"Prediction POST end at {DateTime.Now}, total elapsed: {stopwatch.ElapsedMilliseconds}" };
        stopwatch.Stop();
        stopwatch.Restart();
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostAsync)} (Championship - Predicton POST) end: {DateTime.Now}");
        return RedirectToPage(new { id, logs });
    }

    public async Task<IActionResult> OnPostUpdateResultAsync(Guid id)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsAdmin)
            return Forbid();

        var match = await _matchService.GetMatchByIdAsync(MatchId);
        if (match == null || match.StartTimeUtc > DateTime.UtcNow)
            return BadRequest("Cannot update result before match starts.");

        var parts = ResultInput.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var home) || !int.TryParse(parts[1], out var away))
        {
            ModelState.AddModelError(string.Empty, "Invalid result format. Use X:Y. Omg you are admin user, you should have known that.");
            return await OnGetAsync(id); // Redisplay page with errors
        }

        await _matchService.UpdateMatchResultAsync(MatchId, home, away);
        await _scoringService.RecalculateForMatchAsync(MatchId);

        var logs = new List<string>() { $"Result POST end at {DateTime.Now}, total elapsed: {stopwatch.ElapsedMilliseconds}" };
        stopwatch.Stop();
        stopwatch.Restart();
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostUpdateResultAsync)} (Championship - Result POST) start: {DateTime.Now}");
        return RedirectToPage(new { id, logs });
    }

    public async Task<IActionResult> OnPostDeleteMatchAsync(Guid matchId, Guid id)
    {
        await _predictionService.RemovePredictionsForMatchAsync(matchId);
        await _matchService.RemoveMatchAsync(matchId);

        return RedirectToPage(new { id });
    }
}
