using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Kajsmentkeri.Application.Interfaces;

namespace Kajsmentkeri.Web.Pages.Championships;

public class ListModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IChampionshipService _championshipService;
    private readonly ILogger<ListModel> _logger;

    public ListModel(UserManager<AppUser> userManager, IChampionshipService championshipService, ILogger<ListModel> logger)
    {
        _userManager = userManager;
        _championshipService = championshipService;
        _logger = logger;
    }

    public List<ChampionshipViewModel> Championships { get; set; } = new();
    public bool IsAdmin { get; set; }

    public async Task OnGetAsync()
    {
        _logger.LogInformation($"{nameof(ListModel)}.{nameof(OnGetAsync)} (Championship) start: {DateTime.Now}");

        var championships = await _championshipService.GetAllAsync();

        _logger.LogInformation($"{nameof(ListModel)}.{nameof(OnGetAsync)} (Championship) championship loaded: {DateTime.Now}");

        // Fetch all users once
        var users = await _userManager.Users.ToListAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u.UserName ?? "Unknown");

        Championships = championships.Select(c => new ChampionshipViewModel
        {
            Id = c.Id,
            Name = c.Name,
            Year = c.Year,
            CreatedByUserName = userMap.TryGetValue(c.CreatedById, out var username)
                ? username
                : "Unknown"
        }).ToList();

        var currentUser = await _userManager.GetUserAsync(User);
        IsAdmin = currentUser?.IsAdmin == true;

        _logger.LogInformation($"{nameof(ListModel)}.{nameof(OnGetAsync)} (Championship) end: {DateTime.Now}");
    }


    public class ChampionshipViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Year { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
    }
}
