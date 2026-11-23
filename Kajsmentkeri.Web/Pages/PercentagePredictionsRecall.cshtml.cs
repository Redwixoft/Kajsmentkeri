using Kajsmentkeri.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages;

[Authorize]
public class PercentagePredictionsRecallModel : PageModel
{
    private readonly IPercentagePredictionService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;

    public PercentagePredictionsRecallModel(
        IPercentagePredictionService service,
        ICurrentUserService currentUserService,
        IConfiguration configuration)
    {
        _service = service;
        _currentUserService = currentUserService;
        _configuration = configuration;
    }

    [BindProperty]
    public List<int?> Answers { get; set; } = new();

    public IReadOnlyDictionary<int, string> Questions { get; private set; } = new Dictionary<int, string>();
    public IReadOnlyDictionary<int, bool?> Outcomes { get; private set; } = new Dictionary<int, bool?>();
    public bool HasOriginal { get; private set; }
    public bool HasRecall { get; private set; }
    public bool IsConclusionRevealed { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_currentUserService.UserId is not Guid userId)
            return Challenge();

        await LoadAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_currentUserService.UserId is not Guid userId)
            return Challenge();

        for (var i = 0; i < IPercentagePredictionService.QuestionCount; i++)
        {
            var value = i < Answers.Count ? Answers[i] : null;
            if (value is null or < 0 or > 100)
                ModelState.AddModelError($"Answers[{i}]", $"Otázka {i + 1}: zadaj číslo od 0 do 100.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await _service.SubmitRecallAsync(userId, Answers.Select(a => a!.Value).ToList());
                return RedirectToPage(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        await LoadAsync(userId);
        return Page();
    }

    private async Task LoadAsync(Guid userId)
    {
        var status = await _service.GetRecallStatusAsync(userId);
        HasOriginal = status.HasOriginal;
        HasRecall = status.HasRecall;
        IsConclusionRevealed = status.IsConclusionRevealed;
        Questions = PercentageQuestions.Load(_configuration);
        Outcomes = await _service.GetOutcomesAsync();
    }
}
