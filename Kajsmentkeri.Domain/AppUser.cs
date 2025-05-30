using Microsoft.AspNetCore.Identity;

namespace Kajsmentkeri.Domain;

public class AppUser : IdentityUser<Guid>
{
    public bool IsAdmin { get; set; }
}
