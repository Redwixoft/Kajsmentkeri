using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages;

[Authorize]
public class PercentagePredictionsModel : PageModel
{
    private readonly IPercentagePredictionService _percentagePredictionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;

    public PercentagePredictionsModel(
        IPercentagePredictionService percentagePredictionService,
        ICurrentUserService currentUserService,
        IConfiguration configuration)
    {
        _percentagePredictionService = percentagePredictionService;
        _currentUserService = currentUserService;
        _configuration = configuration;
    }

    [BindProperty]
    public PercentagePredictionDto Input { get; set; } = new();

    public Dictionary<string, string> Questions { get; private set; } = new();
    public bool AlreadySubmitted { get; private set; }
    public bool Success { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_currentUserService.UserId == null)
            return Challenge();

        AlreadySubmitted = await _percentagePredictionService.HasUserSubmittedAsync(_currentUserService.UserId.Value);
        LoadQuestions();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_currentUserService.UserId == null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            LoadQuestions();
            return Page();
        }

        try
        {
            await _percentagePredictionService.SubmitPredictionsAsync(_currentUserService.UserId.Value, Input);
            Success = true;
            return RedirectToPage(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            LoadQuestions();
            return Page();
        }
    }

    private void LoadQuestions()
    {
        var section = _configuration.GetSection("PercentagePredictions:Questions");
        foreach (var child in section.GetChildren())
        {
            Questions[child.Key] = child.Value ?? string.Empty;
        }
    }
}
