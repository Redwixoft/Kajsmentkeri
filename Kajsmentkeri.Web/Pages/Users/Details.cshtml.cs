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
    public UserPredictionProfileDto? PredictionProfile { get; set; }
    public ChampionshipType? SportType { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, ChampionshipType? sportType = null)
    {
        SportType = sportType;

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return NotFound();

        ProfileUser = user;

        var globalStatsTask    = _leaderboardService.GetGlobalLeaderboardAsync(sportType);
        var medalTask          = _leaderboardService.GetMedalCountsAsync(sportType);
        var recordsTask        = _leaderboardService.GetUserPersonalRecordsAsync(id, sportType);
        var champStatsTask     = _leaderboardService.GetUserChampionshipStatsAsync(id, sportType);
        var profileTask        = _leaderboardService.GetUserPredictionProfileAsync(id, sportType);

        var globalStats = await globalStatsTask;
        Stats = globalStats.FirstOrDefault(s => s.UserId == id);

        var medalCounts = await medalTask;
        Medals = medalCounts.TryGetValue(id, out var medals) ? medals : new();

        PersonalRecords   = await recordsTask;
        ChampionshipStats = await champStatsTask;
        PredictionProfile = await profileTask;

        return Page();
    }
}
