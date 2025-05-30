using System.Security.Claims;
using Kajsmentkeri.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Kajsmentkeri.Web.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var id = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(id, out var guid) ? guid : null;
        }
    }

    public string? UserName =>
        _httpContextAccessor.HttpContext?.User.Identity?.Name;

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin =>
        _httpContextAccessor.HttpContext?.User?.Claims.Any(c =>
            c.Type == "IsAdmin" && c.Value == "True") ?? false;
}
