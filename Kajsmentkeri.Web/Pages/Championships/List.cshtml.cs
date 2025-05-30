using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Web.Pages.Championships;

public class ListModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public ListModel(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public List<ChampionshipViewModel> Championships { get; set; } = new();
    public bool IsAdmin { get; set; }

    public async Task OnGetAsync()
    {
        var championships = await _db.Championships
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

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
    }


    public class ChampionshipViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Year { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
    }
}
