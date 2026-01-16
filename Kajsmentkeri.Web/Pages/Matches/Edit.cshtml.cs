using System.ComponentModel.DataAnnotations;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages.Matches;

[Authorize]
public class EditModel : PageModel
{
    private readonly IMatchService _matchService;
    private readonly UserManager<AppUser> _userManager;

    public EditModel(UserManager<AppUser> userManager, IMatchService matchService)
    {
        _userManager = userManager;
        _matchService = matchService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public Guid MatchId { get; set; }

    public Guid ChampionshipId { get; set; }

    public class InputModel
    {
        [Required]
        public string HomeTeam { get; set; } = string.Empty;

        [Required]
        public string AwayTeam { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.IsAdmin)
            return Forbid();

        var match = await _matchService.GetMatchByIdAsync(id);
        if (match == null)
            return NotFound();

        MatchId = id;
        ChampionshipId = match.ChampionshipId;
        Input = new InputModel
        {
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            StartTime = match.StartTimeUtc
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.IsAdmin)
            return Forbid();

        if (!ModelState.IsValid)
            return Page();

        var match = await _matchService.GetMatchByIdAsync(MatchId);
        if (match == null)
            return NotFound();

        await _matchService.UpdateMatchAsync(MatchId, Input.HomeTeam, Input.AwayTeam, Input.StartTime);

        return RedirectToPage("/Championships/Edit", new { id = match.ChampionshipId });
    }
}
