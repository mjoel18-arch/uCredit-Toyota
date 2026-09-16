using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using UCredit.Application.Execution;

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
    IReadOnlyList<string> PermissionCodes,
    IReadOnlyList<int>? AllowedCompanyIds = null);

public interface IDeploymentTenantPolicy
{
    string? TenantCode { get; }

    bool IsAllowed(string tenantCode);
}

public sealed class DeploymentTenantPolicy : IDeploymentTenantPolicy
{
    public DeploymentTenantPolicy(IConfiguration configuration)
        : this(configuration["Deployment:TenantCode"])
    {
    }

    public DeploymentTenantPolicy(string? tenantCode)
    {
        TenantCode = string.IsNullOrWhiteSpace(tenantCode) ? null : tenantCode.Trim();
    }

    public string? TenantCode { get; }

    public bool IsAllowed(string tenantCode) =>
        TenantCode is not null &&
        !string.IsNullOrWhiteSpace(tenantCode) &&
        string.Equals(TenantCode, tenantCode.Trim(), StringComparison.OrdinalIgnoreCase);
}

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

public sealed class IdentityExecutionTenantContext(
    IHttpContextAccessor httpContextAccessor,
    ITenantMembershipStore membershipStore,
    IDeploymentTenantPolicy deploymentTenantPolicy) : IExecutionTenantContext
{
    public async Task<ExecutionTenant?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (deploymentTenantPolicy.TenantCode is null)
        {
            return null;
        }

        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(principal.FindFirstValue(TenantClaimTypes.Id), out var tenantId))
        {
            return null;
        }

        var tenantCode = principal.FindFirstValue(TenantClaimTypes.Code);
        if (tenantCode is null || !deploymentTenantPolicy.IsAllowed(tenantCode))
        {
            return null;
        }

        var membership = await membershipStore.FindActiveMembershipAsync(
            userId,
            tenantId,
            tenantCode,
            cancellationToken);
        if (membership is null ||
            !deploymentTenantPolicy.IsAllowed(membership.TenantCode) ||
            membership.AllowedCompanyIds is not { Count: > 0 })
        {
            return null;
        }

        return new ExecutionTenant(
            membership.TenantId,
            membership.TenantCode,
            membership.AllowedCompanyIds
                .Distinct()
                .Order()
                .ToArray());
    }
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
