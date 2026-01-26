using System.ComponentModel.DataAnnotations;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages.Championships;

[Authorize]
public class EditModel : PageModel
{
    private readonly IChampionshipService _championshipService;
    private readonly IMatchService _matchService;
    private readonly UserManager<AppUser> _userManager;

    public EditModel(UserManager<AppUser> userManager, IChampionshipService championshipService, IMatchService matchService)
    {
        _userManager = userManager;
        _championshipService = championshipService;
        _matchService = matchService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Guid Id { get; set; }
    public List<MatchViewModel> Matches { get; set; } = new();

    public class InputModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(1900, 2100)]
        public int Year { get; set; }

        public string? Description { get; set; }

        [Display(Name = "Points for correct winner")]
        [Range(0, 10)]
        public int PointsForCorrectWinner { get; set; } = 3;

        [Display(Name = "Points for exact score")]
        [Range(0, 10)]
        public int PointsForExactScore { get; set; } = 2;

        [Display(Name = "Points for only correct winner")]
        [Range(0, 10)]
        public int PointsForOnlyCorrectWinner { get; set; } = 2;

        [Display(Name = "Points for rarity")]
        [Range(0, 10)]
        public int RarityPointsBonus { get; set; } = 0;

        [Display(Name = "Enforce leaderboard-based prediction visibility")]
        public bool EnforceLeaderboardVisibilityRules { get; set; }
    }

    public class MatchViewModel
    {
        public Guid Id { get; set; }
        public string Teams { get; set; } = string.Empty;
        public DateTime StartTimeUtc { get; set; }
        public string? Result { get; set; }
        public int PredictionCount { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.IsAdmin != true)
            return Forbid();

        var championshipTask = _championshipService.GetByIdAsync(id);
        var matchesTask = _matchService.GetMatchesByChampionshipAsync(id);

        var championship = await championshipTask;
        if (championship == null)
            return NotFound();

        Id = id;
        Input = new InputModel
        {
            Name = championship.Name,
            Year = championship.Year,
            Description = championship.Description,
            PointsForCorrectWinner = championship.ScoringRules?.PointsForCorrectWinner ?? 3,
            PointsForExactScore = championship.ScoringRules?.PointsForExactScore ?? 2,
            PointsForOnlyCorrectWinner = championship.ScoringRules?.PointsForOnlyCorrectWinner ?? 2,
            RarityPointsBonus = championship.ScoringRules?.RarityPointsBonus ?? 0,
            EnforceLeaderboardVisibilityRules = championship.EnforceLeaderboardVisibilityRules
        };

        var matches = await matchesTask;
        Matches = matches.Select(m => new MatchViewModel
        {
            Id = m.Id,
            Teams = $"{m.HomeTeam} - {m.AwayTeam}",
            StartTimeUtc = m.StartTimeUtc,
            Result = m.HomeScore.HasValue && m.AwayScore.HasValue ? $"{m.HomeScore}:{m.AwayScore}" : "-",
            PredictionCount = m.Predictions.Count // This assumes Matches included Predictions. MatchService.GetMatchesByChampionshipAsync doesn't yet.
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            // Reload matches if validation fails
            return await OnGetAsync(id);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user?.IsAdmin != true)
            return Forbid();

        var championship = await _championshipService.GetByIdAsync(id);
        if (championship == null)
            return NotFound();

        championship.Name = Input.Name;
        championship.Year = Input.Year;
        championship.Description = Input.Description;
        championship.EnforceLeaderboardVisibilityRules = Input.EnforceLeaderboardVisibilityRules;

        if (championship.ScoringRules == null)
        {
            championship.ScoringRules = new ChampionshipScoringRules { ChampionshipId = id };
        }

        championship.ScoringRules.PointsForCorrectWinner = Input.PointsForCorrectWinner;
        championship.ScoringRules.PointsForExactScore = Input.PointsForExactScore;
        championship.ScoringRules.PointsForOnlyCorrectWinner = Input.PointsForOnlyCorrectWinner;
        championship.ScoringRules.RarityPointsBonus = Input.RarityPointsBonus;

        await _championshipService.UpdateChampionshipAsync(championship);

        return RedirectToPage("/Championships/List");
    }

    public async Task<IActionResult> OnPostDeleteMatchAsync(Guid id, Guid matchId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.IsAdmin != true)
            return Forbid();

        await _matchService.RemoveMatchAsync(matchId);
        return RedirectToPage(new { id });
    }
}
