using System.Security.Claims;

namespace OnTimeCRM.Application.Common;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("sub claim missing"));

    public static Guid? TryGetCompanyId(this ClaimsPrincipal principal)
    {
        var val = principal.FindFirst("cid")?.Value;
        return val is null ? null : Guid.Parse(val);
    }

    public static Guid GetCompanyId(this ClaimsPrincipal principal) =>
        TryGetCompanyId(principal)
            ?? throw new InvalidOperationException("cid claim missing");

    public static Guid? TryGetBrandId(this ClaimsPrincipal principal)
    {
        var val = principal.FindFirst("bid")?.Value;
        return val is null ? null : Guid.Parse(val);
    }

    public static Guid GetBrandId(this ClaimsPrincipal principal) =>
        TryGetBrandId(principal)
            ?? throw new InvalidOperationException("bid claim missing");

    public static int GetRole(this ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirst(ClaimTypes.Role)?.Value
            ?? throw new InvalidOperationException("role claim missing"));

    public static bool IsManager(this ClaimsPrincipal principal) =>
        principal.GetRole() == 1;
}
