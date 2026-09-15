using System.Security.Claims;

namespace UCredit.Infrastructure.Identity.Tenants;

public static class TenantClaimTypes
{
    public const string Id = "tenant_id";
    public const string Code = "tenant_code";
}

public sealed record ActiveTenantMembership(
    Guid TenantId,
    string TenantCode,
    string TenantName,
    IReadOnlyList<string> PermissionCodes);

public interface ITenantMembershipStore
{
    Task<Models.ApplicationUser?> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActiveTenantMembership>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ActiveTenantMembership?> FindActiveMembershipAsync(
        Guid userId,
        string tenantCode,
        CancellationToken cancellationToken = default);

    Task<ActiveTenantMembership?> FindActiveMembershipAsync(
        Guid userId,
        Guid tenantId,
        string tenantCode,
        CancellationToken cancellationToken = default);
}

public interface ITenantCookieIssuer
{
    Task<bool> IssueAsync(
        ClaimsPrincipal principal,
        ActiveTenantMembership membership,
        CancellationToken cancellationToken = default);
}

public static class TenantSelectionClaims
{
    public static IReadOnlyList<Claim> Build(ActiveTenantMembership membership)
    {
        var claims = new List<Claim>
        {
            new(TenantClaimTypes.Id, membership.TenantId.ToString("D")),
            new(TenantClaimTypes.Code, membership.TenantCode)
        };
        claims.AddRange(membership.PermissionCodes.Select(code => new Claim("permission", code)));
        return claims;
    }
}