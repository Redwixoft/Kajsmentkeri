using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages.Users;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ILeaderboardService _leaderboardService;

    public DetailsModel(UserManager<AppUser> userManager, ILeaderboardService leaderboardService)
    {
        _userManager = userManager;
        _leaderboardService = leaderboardService;
    }

    public AppUser ProfileUser { get; set; } = null!;
    public LeaderboardEntryDto? Stats { get; set; }
    public List<(int Position, string ChampionshipName, int Year)> Medals { get; set; } = new();
    public ChampionshipRecordsDto PersonalRecords { get; set; } = new();
    public List<UserChampionshipStatsDto> ChampionshipStats { get; set; } = new();
    public ChampionshipType SportType { get; set; } = ChampionshipType.IceHockey;

    public async Task<IActionResult> OnGetAsync(Guid id, ChampionshipType sportType = ChampionshipType.IceHockey)
    {
        SportType = sportType;

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return NotFound();

        ProfileUser = user;

        var globalStats = await _leaderboardService.GetGlobalLeaderboardAsync(sportType);
        Stats = globalStats.FirstOrDefault(s => s.UserId == id);

        var medalCounts = await _leaderboardService.GetMedalCountsAsync(sportType);
        Medals = medalCounts.TryGetValue(id, out var medals) ? medals : new();

        PersonalRecords = await _leaderboardService.GetUserPersonalRecordsAsync(id, sportType);
        ChampionshipStats = await _leaderboardService.GetUserChampionshipStatsAsync(id, sportType);

        return Page();
    }
}
