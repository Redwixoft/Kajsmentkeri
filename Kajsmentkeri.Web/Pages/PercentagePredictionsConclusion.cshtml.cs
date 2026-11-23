using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages;

[Authorize]
public class PercentagePredictionsConclusionModel : PageModel
{
    private readonly IPercentagePredictionService _service;
    private readonly IConfiguration _configuration;

    public PercentagePredictionsConclusionModel(IPercentagePredictionService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    public PercentagePredictionConclusionDto Conclusion { get; private set; } = new();
    public IReadOnlyDictionary<int, string> Questions { get; private set; } = new Dictionary<int, string>();

    public async Task OnGetAsync()
    {
        Conclusion = await _service.GetConclusionAsync();
        Questions = PercentageQuestions.Load(_configuration);
    }
}
