using System.ComponentModel.DataAnnotations;
using System.Linq;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Web.Pages.Matches;

[Authorize]
public class EditModel : PageModel
{
    private readonly IMatchService _matchService;
    private readonly IPredictionService _predictionService;
    private readonly IPredictionScoringService _scoringService;
    private readonly UserManager<AppUser> _userManager;

    public EditModel(UserManager<AppUser> userManager, IMatchService matchService, IPredictionService predictionService, IPredictionScoringService scoringService)
    {
        _userManager = userManager;
        _matchService = matchService;
        _predictionService = predictionService;
        _scoringService = scoringService;
    }

    [BindProperty(SupportsGet = true)]
    public Guid MatchId { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public Guid ChampionshipId { get; set; }

    public List<PredictionViewModel> MatchPredictions { get; set; } = new();
    public List<AppUser> AvailableUsers { get; set; } = new();

    [BindProperty]
    public NewPredictionModel NewPrediction { get; set; } = new();

    public class InputModel
    {
        [Required]
        public string HomeTeam { get; set; } = string.Empty;

        [Required]
        public string AwayTeam { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }
    }

    public class PredictionViewModel
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
    }

    public class NewPredictionModel
    {
        [Required]
        public Guid UserId { get; set; }
        [Required]
        [Range(0, 20)]
        public int HomeScore { get; set; }
        [Required]
        [Range(0, 20)]
        public int AwayScore { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid matchId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.IsAdmin)
            return Forbid();

        var match = await _matchService.GetMatchByIdAsync(matchId);
        if (match == null)
            return NotFound();

        MatchId = matchId;
        ChampionshipId = match.ChampionshipId;
        
        Input.HomeTeam = match.HomeTeam;
        Input.AwayTeam = match.AwayTeam;
        Input.StartTime = match.StartTimeUtc;

        await LoadPredictionsAsync();

        return Page();
    }

    private async Task LoadPredictionsAsync()
    {
        if (ChampionshipId == Guid.Empty)
        {
             var match = await _matchService.GetMatchByIdAsync(MatchId);
             if (match != null) ChampionshipId = match.ChampionshipId;
        }

        var predictionsTask = _predictionService.GetPredictionsForChampionshipAsync(ChampionshipId);
        var usersTask = _userManager.Users.ToListAsync();

        var matchesPredictions = await predictionsTask;
        var predictionsForMatch = matchesPredictions.Where(p => p.MatchId == MatchId).ToList();
        var allUsers = await usersTask;

        var predictedUserIds = predictionsForMatch.Select(p => p.UserId).ToHashSet();

        MatchPredictions = predictionsForMatch.Select(p => new PredictionViewModel
        {
            UserId = p.UserId,
            UserName = allUsers.FirstOrDefault(u => u.Id == p.UserId)?.UserName ?? "Unknown",
            HomeScore = p.PredictedHome,
            AwayScore = p.PredictedAway
        }).OrderBy(p => p.UserName).ToList();

        AvailableUsers = allUsers.Where(u => !predictedUserIds.Contains(u.Id)).OrderBy(u => u.UserName).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.IsAdmin)
            return Forbid();

        if (!ModelState.IsValid)
        {
            await LoadPredictionsAsync();
            return Page();
        }

        var match = await _matchService.GetMatchByIdAsync(MatchId);
        if (match == null)
            return NotFound();

        await _matchService.UpdateMatchAsync(MatchId, Input.HomeTeam, Input.AwayTeam, Input.StartTime);

        return RedirectToPage("/Championships/Edit", new { id = match.ChampionshipId });
    }

    public async Task<IActionResult> OnPostAddPredictionAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.IsAdmin)
            return Forbid();

        ModelState.Remove("HomeTeam");
        ModelState.Remove("AwayTeam");
        ModelState.Remove("StartTime");

        if (!ModelState.IsValid)
        {
            await LoadPredictionsAsync();
            return Page();
        }

        await _predictionService.SetPredictionAsync(MatchId, NewPrediction.UserId, NewPrediction.HomeScore, NewPrediction.AwayScore);
        
        var match = await _matchService.GetMatchByIdAsync(MatchId);
        if (match?.HomeScore != null)
        {
            await _scoringService.RecalculateForMatchAsync(MatchId);
        }

        return RedirectToPage(new { matchId = MatchId });
    }

    public async Task<IActionResult> OnPostUpdatePredictionAsync(Guid userId, int homeScore, int awayScore)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.IsAdmin)
            return Forbid();

        ModelState.Remove("Input.HomeTeam");
        ModelState.Remove("Input.AwayTeam");
        ModelState.Remove("Input.StartTime");

        await _predictionService.SetPredictionAsync(MatchId, userId, homeScore, awayScore);

        var match = await _matchService.GetMatchByIdAsync(MatchId);
        if (match?.HomeScore != null)
        {
            await _scoringService.RecalculateForMatchAsync(MatchId);
        }

        return RedirectToPage(new { matchId = MatchId });
    }
}
