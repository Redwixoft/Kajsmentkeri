using System.ComponentModel.DataAnnotations;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages.Matches;

[Authorize]
public class AddModel : PageModel
{
    private readonly IMatchService _matchService;
    private readonly UserManager<AppUser> _userManager;

    private readonly ITimeService _timeService;

    public AddModel(UserManager<AppUser> userManager, IMatchService matchService, ITimeService timeService)
    {
        _userManager = userManager;
        _matchService = matchService;
        _timeService = timeService;
    }

    [BindProperty(SupportsGet = true)]
    public Guid ChampionshipId { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        public string HomeTeam { get; set; } = string.Empty;

        [Required]
        public string AwayTeam { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }
    }

    public IActionResult OnGet()
    {
        // Optional: verify that Championship exists
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || !currentUser.IsAdmin)
            return Forbid();

        if (!ModelState.IsValid)
            return Page();

        await _matchService.CreateMatchAsync(ChampionshipId, Input.HomeTeam, Input.AwayTeam, Input.StartTime);

        return RedirectToPage("/Championships/Details", new { id = ChampionshipId });
    }
}
