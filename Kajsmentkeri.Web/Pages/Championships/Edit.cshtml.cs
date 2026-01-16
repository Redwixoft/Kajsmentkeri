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
    private readonly UserManager<AppUser> _userManager;

    public EditModel(UserManager<AppUser> userManager, IChampionshipService championshipService)
    {
        _userManager = userManager;
        _championshipService = championshipService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Guid Id { get; set; }

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
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.IsAdmin != true)
            return Forbid();

        var championship = await _championshipService.GetByIdAsync(id);
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
            RarityPointsBonus = championship.ScoringRules?.RarityPointsBonus ?? 0
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.GetUserAsync(User);
        if (user?.IsAdmin != true)
            return Forbid();

        var championship = await _championshipService.GetByIdAsync(id);
        if (championship == null)
            return NotFound();

        championship.Name = Input.Name;
        championship.Year = Input.Year;
        championship.Description = Input.Description;

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
}
