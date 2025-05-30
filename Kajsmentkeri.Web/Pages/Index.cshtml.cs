using Kajsmentkeri.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kajsmentkeri.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IChampionshipService _championshipService;

        public IndexModel(IChampionshipService championshipService)
        {
            _championshipService = championshipService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var latest = await _championshipService.GetLatestAsync();

            if (latest == null)
                return RedirectToPage("/Championships/List");

            return RedirectToPage("/Championships/Details", new { id = latest.Id });
        }
    }

}
