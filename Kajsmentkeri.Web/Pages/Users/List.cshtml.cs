using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Web.Pages.Users;

[Authorize]
public class ListModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IPredictionService _predictionService;

    public ListModel(UserManager<AppUser> userManager, IPredictionService predictionService)
    {
        _userManager = userManager;
        _predictionService = predictionService;
    }

    public List<AppUser> Users { get; set; } = new();
    public bool IsCurrentUserAdmin { get; set; }

    public async Task OnGetAsync()
    {
        Users = await _userManager.Users.ToListAsync();
        
        var user = await _userManager.GetUserAsync(User);
        IsCurrentUserAdmin = user?.IsAdmin == true;
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.IsAdmin != true)
        {
            return Forbid();
        }

        if (currentUser.Id == id)
        {
             ModelState.AddModelError(string.Empty, "You cannot delete yourself.");
             return await OnGetAsync(currentUser); 
        }

        var userToDelete = await _userManager.FindByIdAsync(id.ToString());
        if (userToDelete != null)
        {
             await _predictionService.RemovePredictionsForUserAsync(id);
             await _userManager.DeleteAsync(userToDelete);
        }

        return RedirectToPage();
    }

    private async Task<IActionResult> OnGetAsync(AppUser currentUser)
    {
        // Re-load data for the page if we are staying on it due to error
         Users = await _userManager.Users.ToListAsync();
         IsCurrentUserAdmin = currentUser.IsAdmin;
         return Page();
    }
}
