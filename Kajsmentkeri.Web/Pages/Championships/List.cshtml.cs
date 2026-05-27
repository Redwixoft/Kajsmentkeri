using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Kajsmentkeri.Application.Interfaces;

namespace Kajsmentkeri.Web.Pages.Championships;

public class ListModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IChampionshipService _championshipService;
    private readonly ILeaderboardService _leaderboardService;
    private readonly ILogger<ListModel> _logger;

    public ListModel(UserManager<AppUser> userManager, IChampionshipService championshipService, ILeaderboardService leaderboardService, ILogger<ListModel> logger)
    {
        _userManager = userManager;
        _championshipService = championshipService;
        _leaderboardService = leaderboardService;
        _logger = logger;
    }

    public List<ChampionshipViewModel> Championships { get; set; } = new();
    public bool IsAdmin { get; set; }
    public ChampionshipType? SportType { get; set; }
    public GlobalStatsDto Stats { get; set; } = new();
    public string? CurrentUserName { get; set; }
    public string CurrentSort { get; set; } = string.Empty;
    public string NameSort { get; set; } = string.Empty;
    public string YearSort { get; set; } = string.Empty;
    public string WinnersSort { get; set; } = string.Empty;
    public string LuckersSort { get; set; } = string.Empty;
    public string OnlyOnesSort { get; set; } = string.Empty;

    public async Task OnGetAsync(string sortOrder = "", ChampionshipType? sportType = null)
    {
        _logger.LogInformation($"{nameof(ListModel)}.{nameof(OnGetAsync)} (Championship) start: {DateTime.UtcNow}");

        CurrentSort = sortOrder;
        NameSort     = sortOrder == "name" ? "name_desc" : "name";
        YearSort     = string.IsNullOrEmpty(sortOrder) ? "year" : "year_desc";
        WinnersSort  = sortOrder == "winners_desc" ? "winners" : "winners_desc";
        LuckersSort  = sortOrder == "luckers_desc" ? "luckers" : "luckers_desc";
        OnlyOnesSort = sortOrder == "onlyones_desc" ? "onlyones" : "onlyones_desc";

        SportType = sportType;
        var championships = await _championshipService.GetAllAsync();

        _logger.LogInformation($"{nameof(ListModel)}.{nameof(OnGetAsync)} (Championship) championship loaded: {DateTime.UtcNow}");

        var currentUser = await _userManager.GetUserAsync(User);
        IsAdmin = currentUser?.IsAdmin == true;
        CurrentUserName = currentUser?.UserName;

        if (!IsAdmin)
        {
            championships = championships.Where(c => !c.IsTest).ToList();
        }

        if (sportType.HasValue)
        {
            championships = championships.Where(c => c.Type == sportType.Value).ToList();
        }

        var allIds = championships.Select(c => c.Id).ToList();
        var endedIds = championships.Where(c => c.IsChampionshipEnded).Select(c => c.Id);
        var standingsTask   = _leaderboardService.GetChampionshipStandingsAsync(endedIds, currentUser?.Id);
        var statsTask       = _leaderboardService.GetGlobalStatsAsync(sportType);
        var aggregatesTask  = _leaderboardService.GetChampionshipAggregatesAsync(allIds);
        var standings  = await standingsTask;
        Stats          = await statsTask;
        var aggregates = await aggregatesTask;

        Championships = championships.Select(c => new ChampionshipViewModel
        {
            Id = c.Id,
            Name = c.Name,
            Year = c.Year,
            IsTest = c.IsTest,
            Type = c.Type,
            Standings = standings.TryGetValue(c.Id, out var s)
                ? s.Select(e => new StandingEntry { Position = e.Position, UserName = e.UserName }).ToList()
                : new List<StandingEntry>(),
            TotalWinners  = aggregates.TryGetValue(c.Id, out var agg) ? agg.TotalWinners  : 0,
            TotalLuckers  = aggregates.TryGetValue(c.Id, out var agg2) ? agg2.TotalLuckers : 0,
            TotalOnlyOnes = aggregates.TryGetValue(c.Id, out var agg3) ? agg3.TotalOnlyOnes : 0,
        }).ToList();

        Championships = sortOrder switch
        {
            "name"         => Championships.OrderBy(c => c.Name).ToList(),
            "name_desc"    => Championships.OrderByDescending(c => c.Name).ToList(),
            "year"         => Championships.OrderBy(c => c.Year).ThenBy(c => c.Name).ToList(),
            "year_desc"    => Championships.OrderByDescending(c => c.Year).ThenBy(c => c.Name).ToList(),
            "winners"      => Championships.OrderBy(c => c.TotalWinners).ToList(),
            "winners_desc" => Championships.OrderByDescending(c => c.TotalWinners).ToList(),
            "luckers"      => Championships.OrderBy(c => c.TotalLuckers).ToList(),
            "luckers_desc" => Championships.OrderByDescending(c => c.TotalLuckers).ToList(),
            "onlyones"     => Championships.OrderBy(c => c.TotalOnlyOnes).ToList(),
            "onlyones_desc"=> Championships.OrderByDescending(c => c.TotalOnlyOnes).ToList(),
            _              => Championships.OrderByDescending(c => c.Year).ThenBy(c => c.Name).ToList(),
        };

        _logger.LogInformation($"{nameof(ListModel)}.{nameof(OnGetAsync)} (Championship) end: {DateTime.UtcNow}");
    }


    public class ChampionshipViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Year { get; set; }
        public bool IsTest { get; set; }
        public ChampionshipType Type { get; set; }
        public List<StandingEntry> Standings { get; set; } = new();
        public int TotalWinners { get; set; }
        public int TotalLuckers { get; set; }
        public int TotalOnlyOnes { get; set; }
    }

    public class StandingEntry
    {
        public int Position { get; set; }
        public string UserName { get; set; } = string.Empty;
    }


    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        await _championshipService.DeleteChampionshipAsync(id);
        return RedirectToPage();
    }
}
