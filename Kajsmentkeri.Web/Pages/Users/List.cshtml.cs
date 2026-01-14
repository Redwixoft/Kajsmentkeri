using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Web.Pages.Users;

[Authorize]
public class ListModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IPredictionService _predictionService;
    private readonly ILeaderboardService _leaderboardService;

    public ListModel(UserManager<AppUser> userManager, IPredictionService predictionService, ILeaderboardService leaderboardService)
    {
        _userManager = userManager;
        _predictionService = predictionService;
        _leaderboardService = leaderboardService;
    }

    public List<AppUser> Users { get; set; } = new();
    public Dictionary<Guid, LeaderboardEntryDto> UserStats { get; set; } = new();
    public bool IsCurrentUserAdmin { get; set; }

    public string NameSort { get; set; } = string.Empty;
    public string EmailSort { get; set; } = string.Empty;
    public string PointsSort { get; set; } = string.Empty;
    public string WinnersSort { get; set; } = string.Empty;
    public string MissesSort { get; set; } = string.Empty;
    public string LuckersSort { get; set; } = string.Empty;
    public string OnlyOnesSort { get; set; } = string.Empty;
    public string AdminSort { get; set; } = string.Empty;
    public string CurrentSort { get; set; } = string.Empty;

    public async Task OnGetAsync(string sortOrder)
    {
        CurrentSort = sortOrder;
        NameSort = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
        EmailSort = sortOrder == "email" ? "email_desc" : "email";
        PointsSort = sortOrder == "points" ? "points_asc" : "points";
        WinnersSort = sortOrder == "winners" ? "winners_asc" : "winners";
        MissesSort = sortOrder == "misses" ? "misses_asc" : "misses";
        LuckersSort = sortOrder == "luckers" ? "luckers_asc" : "luckers";
        OnlyOnesSort = sortOrder == "onlyones" ? "onlyones_asc" : "onlyones";
        AdminSort = sortOrder == "admin" ? "admin_desc" : "admin";

        var usersQuery = _userManager.Users.AsQueryable();
        var users = await usersQuery.ToListAsync();
        var stats = await _leaderboardService.GetGlobalLeaderboardAsync();
        UserStats = stats.ToDictionary(s => s.UserId, s => s);

        // Sorting logic
        Users = sortOrder switch
        {
            "name_desc" => users.OrderByDescending(u => u.UserName).ToList(),
            "email" => users.OrderBy(u => u.Email).ToList(),
            "email_desc" => users.OrderByDescending(u => u.Email).ToList(),
            "points" => users.OrderByDescending(u => UserStats.TryGetValue(u.Id, out var s) ? s.TotalPoints : 0).ToList(),
            "points_asc" => users.OrderBy(u => UserStats.TryGetValue(u.Id, out var s) ? s.TotalPoints : 0).ToList(),
            "winners" => users.OrderByDescending(u => UserStats.TryGetValue(u.Id, out var s) ? s.CorrectWinners : 0).ToList(),
            "winners_asc" => users.OrderBy(u => UserStats.TryGetValue(u.Id, out var s) ? s.CorrectWinners : 0).ToList(),
            "misses" => users.OrderByDescending(u => UserStats.TryGetValue(u.Id, out var s) ? s.OneGoalMisses : 0).ToList(),
            "misses_asc" => users.OrderBy(u => UserStats.TryGetValue(u.Id, out var s) ? s.OneGoalMisses : 0).ToList(),
            "luckers" => users.OrderByDescending(u => UserStats.TryGetValue(u.Id, out var s) ? s.ExactScores : 0).ToList(),
            "luckers_asc" => users.OrderBy(u => UserStats.TryGetValue(u.Id, out var s) ? s.ExactScores : 0).ToList(),
            "onlyones" => users.OrderByDescending(u => UserStats.TryGetValue(u.Id, out var s) ? s.OnlyCorrect : 0).ToList(),
            "onlyones_asc" => users.OrderBy(u => UserStats.TryGetValue(u.Id, out var s) ? s.OnlyCorrect : 0).ToList(),
            "admin" => users.OrderBy(u => u.IsAdmin).ToList(),
            "admin_desc" => users.OrderByDescending(u => u.IsAdmin).ToList(),
            _ => users.OrderBy(u => u.UserName).ToList(),
        };
        
        var user = await _userManager.GetUserAsync(User);
        IsCurrentUserAdmin = user?.IsAdmin == true;
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        if (currentUser.Id == id)
        {
             ModelState.AddModelError(string.Empty, "You cannot delete yourself.");
             return await OnGetAsync(currentUser); 
        }

        var userToDelete = await _userManager.FindByIdAsync(id.ToString());
        if (userToDelete != null)
        {
             await _predictionService.RemovePredictionsForUserAsync(id);
             await _userManager.DeleteAsync(userToDelete);
        }

        return RedirectToPage();
    }

    private async Task<IActionResult> OnGetAsync(AppUser currentUser)
    {
        // Re-load data for the page if we are staying on it due to error
         await OnGetAsync(string.Empty);
         IsCurrentUserAdmin = currentUser.IsAdmin;
         return Page();
    }
}
