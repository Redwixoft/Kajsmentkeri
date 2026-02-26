using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Kajsmentkeri.Web.Pages.Admin;

[Authorize]
public class ImportChampionshipModel : PageModel
{
    private readonly IImportService _importService;
    private readonly UserManager<AppUser> _userManager;

    public ImportChampionshipModel(IImportService importService, UserManager<AppUser> userManager)
    {
        _importService = importService;
        _userManager = userManager;
    }

    [BindProperty]
    [Required]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public string ChampionshipName { get; set; } = "Imported Championship";

    [BindProperty]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty]
    public string? Description { get; set; } = "Imported Championship";

    [BindProperty]
    public ChampionshipType Type { get; set; } = ChampionshipType.IceHockey;

    [BindProperty]
    [Range(0, 10)]
    public int PointsForCorrectWinner { get; set; } = 3;

    [BindProperty]
    [Range(0, 10)]
    public int PointsForExactScore { get; set; } = 2;

    [BindProperty]
    [Range(0, 10)]
    public int PointsForOnlyCorrectWinner { get; set; } = 2;

    [BindProperty]
    [Range(0.0, 10.0)]
    public decimal RarityPointsBonus { get; set; } = 0;

    [BindProperty]
    [Range(0, 10)]
    public int PointsForChampionshipWinner { get; set; } = 3;

    [BindProperty]
    [Range(0, 10)]
    public int PointsForChampionshipRunnerUp { get; set; } = 2;

    [BindProperty]
    [Range(0, 10)]
    public int PointsForChampionshipThirdPlace { get; set; } = 1;

    public ImportAnalysisResultViewModel? Analysis { get; set; }

    public List<AppUser> Users { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsAdmin)
        {
            return Forbid(); // Or redirect
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsAdmin) return Forbid();

        if (Upload == null || Upload.Length == 0)
        {
            ModelState.AddModelError("Upload", "Please select a file.");
            return Page();
        }

        using var stream = Upload.OpenReadStream();
        var result = await _importService.AnalyzeImportAsync(stream);

        Analysis = new ImportAnalysisResultViewModel
        {
            UserMappings = result.UserMappings,
            TeamMappings = result.TeamMappings,
            PreviewMatches = result.PreviewMatches,
            PredictionFileBase64 = Convert.ToBase64String(result.FileContent)
        };

        Users = await _userManager.Users.OrderBy(u => u.UserName).ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync(
        ImportAnalysisResultViewModel Analysis, 
        Dictionary<string, string> UserMapInput,
        Dictionary<string, string> TeamMapInput)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsAdmin) return Forbid();

        // Reconstruct Maps
        var userMap = new Dictionary<string, Guid>();
        foreach (var item in UserMapInput)
        {
            if (Guid.TryParse(item.Value, out Guid userId))
            {
                userMap[item.Key] = userId;
            }
        }

        var teamMap = new Dictionary<string, string>();
        foreach (var item in TeamMapInput)
        {
            if (!string.IsNullOrWhiteSpace(item.Value))
            {
                teamMap[item.Key] = item.Value;
            }
        }

        var fileContent = Convert.FromBase64String(Analysis.PredictionFileBase64);

        var data = new ImportData
        {
            FileContent = fileContent,
            UserMap = userMap,
            TeamMap = teamMap,
            ChampionshipName = ChampionshipName,
            Year = Year,
            Description = Description,
            Type = Type,
            PointsForCorrectWinner = PointsForCorrectWinner,
            PointsForExactScore = PointsForExactScore,
            PointsForOnlyCorrectWinner = PointsForOnlyCorrectWinner,
            RarityPointsBonus = RarityPointsBonus,
            PointsForChampionshipWinner = PointsForChampionshipWinner,
            PointsForChampionshipRunnerUp = PointsForChampionshipRunnerUp,
            PointsForChampionshipThirdPlace = PointsForChampionshipThirdPlace
        };

        await _importService.ImportChampionshipAsync(data);

        return RedirectToPage("/Championships/List"); // Redirect to list
    }

    public class ImportAnalysisResultViewModel
    {
        public List<UserMapping> UserMappings { get; set; } = new();
        public List<TeamMapping> TeamMappings { get; set; } = new();
        public List<string> PreviewMatches { get; set; } = new();
        public string PredictionFileBase64 { get; set; } = string.Empty;
    }
}
