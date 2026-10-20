using System.Security.Claims;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Authentication;

public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;

    public int AgentId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : 0;
        }
    }

    public string FullName => User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public string Email => User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
}
