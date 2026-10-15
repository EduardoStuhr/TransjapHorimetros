using System.Security.Claims;
using TransjapHorimetros.Application.Abstractions;

namespace TransjapHorimetros.Api.Security;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var rawId = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");
            return Guid.TryParse(rawId, out var id) ? id : null;
        }
    }

    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue("email");

    public string? FullName =>
        User?.FindFirstValue(ClaimTypes.Name)
        ?? User?.FindFirstValue("name");

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role)
            .Concat(User?.FindAll("role") ?? [])
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? Array.Empty<string>();

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) =>
        User?.IsInRole(role) == true
        || Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
