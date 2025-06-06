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

        Logs.Add($"OnGetAsync start at {DateTime.Now}");
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnGetAsync)} (Championship) start: {DateTime.Now}");

        Championship = await _championshipService.GetByIdAsync(id);
        if (Championship == null)
            return NotFound();

        Logs.Add(Environment.NewLine + $"Championship loaded, elapsed: {stopwatch.ElapsedMilliseconds}");
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnGetAsync)} (Championship) Championship loaded: {DateTime.Now}");

        var currentUser = await _userManager.GetUserAsync(User);
        IsAdmin = currentUser?.IsAdmin == true;
        CurrentUserId = currentUser?.Id;

        Leaderboard = await _leaderboardService.GetLeaderboardAsync(id);

        Logs.Add(Environment.NewLine + $"Leaderboard loaded, elapsed: {stopwatch.ElapsedMilliseconds}");
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnGetAsync)} (Championship) Leaderboard loaded: {DateTime.Now}");

        // Get matches
        Matches = await _matchService.GetMatchesByChampionshipAsync(id);

        Logs.Add(Environment.NewLine + $"Matches loaded, elapsed: {stopwatch.ElapsedMilliseconds}");
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnGetAsync)} (Championship) Matches loaded: {DateTime.Now}");

        // Get all predictions
        var predictions = await _predictionService.GetPredictionsForChampionshipAsync(id);

        Logs.Add(Environment.NewLine + $"Predictions loaded, elapsed: {stopwatch.ElapsedMilliseconds}");
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnGetAsync)} (Championship) Predictions loaded: {DateTime.Now}");

        var users = await _userManager.Users.ToListAsync();

        Logs.Add(Environment.NewLine + $"Users loaded, elapsed: {stopwatch.ElapsedMilliseconds}");
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnGetAsync)} (Championship) Users loaded: {DateTime.Now}");

        // Sort users: logged-in first, then others alphabetically
        Users = users
            .OrderBy(u => u.Id == currentUser?.Id ? 0 : 1)
            .ThenBy(u => u.UserName)
            .Select(u => new UserColumn
            {
                UserId = u.Id,
                UserName = u.UserName ?? "?"
            })
            .ToList();

        Logs.Add(Environment.NewLine + $"Users sorted, elapsed: {stopwatch.ElapsedMilliseconds}");

        // Build prediction map: (MatchId, UserId) => "3:1"
        PredictionMap = predictions.ToDictionary(
            p => (p.MatchId, p.UserId),
            p => p
        );

        Logs.Add(Environment.NewLine + $"Prediction map built, elapsed: {stopwatch.ElapsedMilliseconds}");
        Graph = await _leaderboardService.GetLeaderboardProgressAsync(id);

        Logs.Add(Environment.NewLine + $"Graph loaded, elapsed: {stopwatch.ElapsedMilliseconds}");
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnGetAsync)} (Championship) end: {DateTime.Now}");

        stopwatch.Stop();
        stopwatch.Restart();
        Logs.Add(Environment.NewLine + $"OnGetAsync end at {DateTime.Now}, total elapsed: {stopwatch.ElapsedMilliseconds}");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var logs = new List<string>() { $"Prediction POST start at {DateTime.Now}" };
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostAsync)} (Championship - Predicton POST) start: {DateTime.Now}");

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
            return await OnGetAsync(id, logs); // redisplay with error
        }

        await _predictionService.SubmitPredictionAsync(MatchId, home, away);

        if (match.AwayScore != null)
        {
            await _scoringService.RecalculateForMatchAsync(match.Id);
        }

        logs.Add(Environment.NewLine + $"Prediction POST end at {DateTime.Now}, total elapsed: {stopwatch.ElapsedMilliseconds}");
        stopwatch.Stop();
        stopwatch.Restart();
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostAsync)} (Championship - Predicton POST) end: {DateTime.Now}");
        return RedirectToPage(new { id, logs });
    }

    public async Task<IActionResult> OnPostUpdateResultAsync(Guid id)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var logs = new List<string>() { $"Result POST start at {DateTime.Now}" };
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostUpdateResultAsync)} (Championship - Result POST) start: {DateTime.Now}");

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
            return await OnGetAsync(id, logs); // Redisplay page with errors
        }

        await _matchService.UpdateMatchResultAsync(MatchId, home, away);
        await _scoringService.RecalculateForMatchAsync(MatchId);

        logs.Add(Environment.NewLine + $"Result POST end at {DateTime.Now}, total elapsed: {stopwatch.ElapsedMilliseconds}");
        stopwatch.Stop();
        stopwatch.Restart();
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostUpdateResultAsync)} (Championship - Result POST) start: {DateTime.Now}");
        return RedirectToPage(new { id, logs });
    }

    public async Task<IActionResult> OnPostDeleteMatchAsync(Guid matchId, Guid id)
    {
        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostDeleteMatchAsync)} (Championship - Delete Match POST) start: {DateTime.Now}");

        await _predictionService.RemovePredictionsForMatchAsync(matchId);
        await _matchService.RemoveMatchAsync(matchId);

        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostDeleteMatchAsync)} (Championship - Delete Match POST) start: {DateTime.Now}");

        return RedirectToPage(new { id });
    }
}
