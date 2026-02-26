using System.ComponentModel.DataAnnotations;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages.Championships;

[Authorize]
public class AddModel : PageModel
{
    private readonly IChampionshipService _championshipService;
    private readonly UserManager<AppUser> _userManager;

    public AddModel(UserManager<AppUser> userManager, IChampionshipService championshipService)
    {
        _userManager = userManager;
        _championshipService = championshipService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

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
        [Range(0.0, 10.0)]
        public decimal RarityPointsBonus { get; set; } = 0;

        [Display(Name = "Enforce leaderboard-based prediction visibility")]
        public bool EnforceLeaderboardVisibilityRules { get; set; }

        [Display(Name = "Is Test Championship")]
        public bool IsTest { get; set; }

        [Display(Name = "Supports Championship Winner Prediction")]
        public bool SupportsChampionshipWinnerPrediction { get; set; }

        [Display(Name = "Points for 1st place prediction")]
        [Range(0, 10)]
        public int PointsForChampionshipWinner { get; set; } = 3;

        [Display(Name = "Points for 2nd place prediction")]
        [Range(0, 10)]
        public int PointsForChampionshipRunnerUp { get; set; } = 2;

        [Display(Name = "Points for 3rd place prediction")]
        [Range(0, 10)]
        public int PointsForChampionshipThirdPlace { get; set; } = 1;

        [Display(Name = "Allow High Confidence Prediction")]
        public bool AllowHighConfidencePrediction { get; set; }

        [Display(Name = "Championship Type")]
        public ChampionshipType Type { get; set; } = ChampionshipType.IceHockey;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        var championshipId = Guid.NewGuid();
        var championship = new Championship
        {
            Id = championshipId,
            Name = Input.Name,
            Year = Input.Year,
            Description = Input.Description,
            EnforceLeaderboardVisibilityRules = Input.EnforceLeaderboardVisibilityRules,
            IsTest = Input.IsTest,
            SupportsChampionshipWinnerPrediction = Input.SupportsChampionshipWinnerPrediction,
            AllowHighConfidencePrediction = Input.AllowHighConfidencePrediction,
            Type = Input.Type,
            CreatedById = user.Id,
            CreatedAt = DateTime.UtcNow,
            ScoringRules = new ChampionshipScoringRules
            {
                Id = Guid.NewGuid(),
                ChampionshipId = championshipId,
                PointsForCorrectWinner = Input.PointsForCorrectWinner,
                PointsForExactScore = Input.PointsForExactScore,
                PointsForOnlyCorrectWinner = Input.PointsForOnlyCorrectWinner,
                RarityPointsBonus = Input.RarityPointsBonus,
                PointsForChampionshipWinner = Input.PointsForChampionshipWinner,
                PointsForChampionshipRunnerUp = Input.PointsForChampionshipRunnerUp,
                PointsForChampionshipThirdPlace = Input.PointsForChampionshipThirdPlace,
                CreatedAt = DateTime.UtcNow
            }
        };

        await _championshipService.CreateChampionshipAsync(championship); // Note: Championship object has IsTest property which is set manually below? 
        // Wait, CreateChampionshipAsync(string...) was updated, but CreateChampionshipAsync(Championship) just saves the object.
        // So I need to set IsTest on the object.


        return RedirectToPage("/Championships/List");
    }
}
