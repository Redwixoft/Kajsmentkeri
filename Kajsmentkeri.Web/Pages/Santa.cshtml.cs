using Kajsmentkeri.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages;

[Authorize]
public class SantaModel : PageModel
{
    private readonly ISecretSantaService _secretSantaService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;

    public SantaModel(
        ISecretSantaService secretSantaService,
        ICurrentUserService currentUserService,
        IConfiguration configuration)
    {
        _secretSantaService = secretSantaService;
        _currentUserService = currentUserService;
        _configuration = configuration;
    }

    public IReadOnlyList<string> Rules { get; private set; } = Array.Empty<string>();
    public string EventDate => _configuration.GetValue<string>("SecretSanta:EventDate") ?? "3rd December";
    public string? EventTime => NullIfBlank(_configuration.GetValue<string>("SecretSanta:EventTime"));
    public string? Venue => NullIfBlank(_configuration.GetValue<string>("SecretSanta:Venue"));
    public string? ErrorMessage { get; private set; }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public async Task<IActionResult> OnGetAsync()
    {
        if (_currentUserService.UserId is not Guid userId)
            return Challenge();

        Rules = await _secretSantaService.GetUserRulesAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_currentUserService.UserId is not Guid userId)
            return Challenge();

        try
        {
            await _secretSantaService.AssignRulesAsync(userId, _currentUserService.UserName ?? userId.ToString());
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }

        // Post/Redirect/Get so a refresh doesn't re-submit
        return RedirectToPage();
    }
}
