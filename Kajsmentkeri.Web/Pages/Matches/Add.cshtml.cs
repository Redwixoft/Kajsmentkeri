using System.ComponentModel.DataAnnotations;
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
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public AddModel(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
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

        var match = new Match
        {
            Id = Guid.NewGuid(),
            ChampionshipId = ChampionshipId,
            HomeTeam = Input.HomeTeam,
            AwayTeam = Input.AwayTeam,
            StartTimeUtc = Input.StartTime.ToUniversalTime()
        };

        _db.Matches.Add(match);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Championships/Details", new { id = ChampionshipId });
    }
}
