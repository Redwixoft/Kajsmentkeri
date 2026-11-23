using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages.Admin;

[Authorize]
public class PercentagePredictionOutcomesModel : PageModel
{
    private readonly IPercentagePredictionService _service;
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _configuration;

    public PercentagePredictionOutcomesModel(
        IPercentagePredictionService service,
        UserManager<AppUser> userManager,
        IConfiguration configuration)
    {
        _service = service;
        _userManager = userManager;
        _configuration = configuration;
    }

    /// <summary>Index = question number − 1. "true" / "false" / "" (unknown).</summary>
    [BindProperty]
    public List<string?> Outcomes { get; set; } = new();

    public IReadOnlyDictionary<int, string> Questions { get; private set; } = new Dictionary<int, string>();
    public IReadOnlyDictionary<int, bool?> Current { get; private set; } = new Dictionary<int, bool?>();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await IsAdminAsync()) return Forbid();

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await IsAdminAsync()) return Forbid();

        var parsed = new Dictionary<int, bool?>();
        for (var i = 0; i < IPercentagePredictionService.QuestionCount; i++)
        {
            var raw = i < Outcomes.Count ? Outcomes[i] : null;
            parsed[i + 1] = bool.TryParse(raw, out var b) ? b : null;
        }

        await _service.SetOutcomesAsync(parsed);
        return RedirectToPage(new { saved = true });
    }

    private async Task<bool> IsAdminAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.IsAdmin == true;
    }

    private async Task LoadAsync()
    {
        Questions = PercentageQuestions.Load(_configuration);
        Current = await _service.GetOutcomesAsync();
    }
}
