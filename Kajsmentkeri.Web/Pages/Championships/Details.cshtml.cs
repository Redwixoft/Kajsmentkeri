using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Application.Models;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Web.Pages.Championships;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IPredictionScoringService _scoringService;
    private readonly ILeaderboardService _leaderboardService;

    public DetailsModel(AppDbContext db, UserManager<AppUser> userManager, IPredictionScoringService scoringService, ILeaderboardService leaderboardService)
    {
        _db = db;
        _userManager = userManager;
        _scoringService = scoringService;
        _leaderboardService = leaderboardService;
    }

    public Championship? Championship { get; set; }
    public bool IsAdmin { get; set; }
    public Guid? CurrentUserId { get; set; }

    public List<Match> Matches { get; set; } = new();
    public List<UserColumn> Users { get; set; } = new();
    public Dictionary<(Guid MatchId, Guid UserId), Prediction> PredictionMap { get; set; } = new();
    public List<LeaderboardEntryDto> Leaderboard { get; set; } = new();
    public LineGraphViewModel Graph { get; set; } = new();

    [BindProperty]
    public Guid MatchId { get; set; }

    [BindProperty]
    public string PredictionInput { get; set; } = string.Empty;

    [BindProperty]
    public string ResultInput { get; set; } = string.Empty;

    public class UserColumn
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Championship = await _db.Championships.FindAsync(id);
        if (Championship == null)
            return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        IsAdmin = currentUser?.IsAdmin == true;
        CurrentUserId = currentUser?.Id;

        Leaderboard = await _leaderboardService.GetLeaderboardAsync(id);

        // Get matches
        Matches = await _db.Matches
            .Where(m => m.ChampionshipId == id)
            .OrderBy(m => m.StartTimeUtc)
            .ToListAsync();

        // Get all predictions
        var predictions = await _db.Predictions
            .Where(p => p.Match.ChampionshipId == id)
            .ToListAsync();

        var users = await _userManager.Users.ToListAsync();

        // Sort users: logged-in first, then others alphabetically
        Users = users
            .OrderBy(u => u.Id == currentUser?.Id ? 0 : 1)
            .ThenBy(u => u.UserName)
            .Select(u => new UserColumn
            {
                UserId = u.Id,
                UserName = u.UserName ?? "?"
            })
            .ToList();

        // Build prediction map: (MatchId, UserId) => "3:1"
        PredictionMap = predictions.ToDictionary(
            p => (p.MatchId, p.UserId),
            p => p
        );

        Graph = await _leaderboardService.GetLeaderboardProgressAsync(id);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var match = await _db.Matches.FindAsync(MatchId);
        if (match == null)
            return BadRequest("Match not found.");

        // Parse input like "3:1"
        var parts = PredictionInput.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var home) || !int.TryParse(parts[1], out var away))
        {
            ModelState.AddModelError(string.Empty, "Invalid format. Use X:Y");
            return await OnGetAsync(id); // redisplay with error
        }

        var existing = await _db.Predictions
            .FirstOrDefaultAsync(p => p.MatchId == MatchId && p.UserId == user.Id);

        if (existing == null)
        {
            // Create new prediction
            var prediction = new Prediction
            {
                Id = Guid.NewGuid(),
                MatchId = MatchId,
                UserId = user.Id,
                PredictedHome = home,
                PredictedAway = away
            };
            _db.Predictions.Add(prediction);
        }
        else
        {
            // Update existing prediction
            existing.PredictedHome = home;
            existing.PredictedAway = away;
        }

        await _db.SaveChangesAsync();

        if (match.AwayScore != null)
        {
            await _scoringService.RecalculateForMatchAsync(match.Id);
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUpdateResultAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsAdmin)
            return Forbid();

        var match = await _db.Matches.FindAsync(MatchId);
        if (match == null || match.StartTimeUtc > DateTime.UtcNow)
            return BadRequest("Cannot update result before match starts.");

        var parts = ResultInput.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var home) || !int.TryParse(parts[1], out var away))
        {
            ModelState.AddModelError(string.Empty, "Invalid result format. Use X:Y. Omg you are admin user, you should have known that.");
            return await OnGetAsync(id); // Redisplay page with errors
        }

        match.HomeScore = home;
        match.AwayScore = away;

        await _db.SaveChangesAsync();
        await _scoringService.RecalculateForMatchAsync(MatchId);
        return RedirectToPage(new { id });
    }
}
