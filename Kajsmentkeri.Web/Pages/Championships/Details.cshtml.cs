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

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var championshipTask = _championshipService.GetByIdAsync(id);
        var leaderboardTask = _leaderboardService.GetLeaderboardAsync(id);
        var matchesTask = _matchService.GetMatchesByChampionshipAsync(id);
        var predictionsTask = _predictionService.GetPredictionsForChampionshipAsync(id);
        var leaderboardProgressTask = _leaderboardService.GetLeaderboardProgressAsync(id);

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

        return Page();
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

        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostAsync)} (Championship - Predicton POST) end: {DateTime.Now}");
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

        _logger.LogInformation($"{nameof(DetailsModel)}.{nameof(OnPostUpdateResultAsync)} (Championship - Result POST) start: {DateTime.Now}");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteMatchAsync(Guid matchId, Guid id)
    {
        await _predictionService.RemovePredictionsForMatchAsync(matchId);
        await _matchService.RemoveMatchAsync(matchId);

        return RedirectToPage(new { id });
    }
}
