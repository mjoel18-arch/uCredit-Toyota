using Microsoft.EntityFrameworkCore;
using UCredit.Infrastructure.Identity.Models;

namespace UCredit.Infrastructure.Identity.Tenants;

public sealed class EfTenantMembershipStore(IdentityDbContext dbContext) : ITenantMembershipStore
{
    public Task<ApplicationUser?> GetActiveUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == userId && user.IsActive, cancellationToken);

    public async Task<IReadOnlyList<ActiveTenantMembership>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await ActiveMembershipQuery(userId).ToListAsync(cancellationToken);
        return memberships.Select(ToResult).ToArray();
    }

    public async Task<ActiveTenantMembership?> FindActiveMembershipAsync(
        Guid userId,
        string tenantCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = tenantCode.Trim();
        if (normalizedCode.Length == 0 || normalizedCode.Length > 64)
        {
            return null;
        }

        var memberships = await ActiveMembershipQuery(userId).ToListAsync(cancellationToken);
        var membership = memberships.SingleOrDefault(candidate =>
            string.Equals(candidate.Tenant.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));

        return membership is null ? null : ToResult(membership);
    }

    public async Task<ActiveTenantMembership?> FindActiveMembershipAsync(
        Guid userId,
        Guid tenantId,
        string tenantCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = tenantCode.Trim();
        if (normalizedCode.Length == 0 || normalizedCode.Length > 64)
        {
            return null;
        }

        var membership = await ActiveMembershipQuery(userId)
            .Where(candidate => candidate.TenantId == tenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return membership is not null &&
            string.Equals(membership.Tenant.Code, normalizedCode, StringComparison.OrdinalIgnoreCase)
            ? ToResult(membership)
            : null;
    }

    private IQueryable<UserTenantMembership> ActiveMembershipQuery(Guid userId) =>
        dbContext.UserTenantMemberships
            .AsNoTracking()
            .Include(membership => membership.Tenant)
                .ThenInclude(tenant => tenant.LegacyCompanyScopes)
            .Include(membership => membership.Permissions)
                .ThenInclude(membershipPermission => membershipPermission.Permission)
            .Where(membership =>
                membership.UserId == userId &&
                membership.IsActive &&
                membership.User.IsActive &&
                membership.Tenant.IsActive);

    private static ActiveTenantMembership ToResult(UserTenantMembership membership) =>
        new(
            membership.TenantId,
            membership.Tenant.Code,
            membership.Tenant.Name,
            membership.Permissions
                .Select(membershipPermission => membershipPermission.Permission.Code)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            membership.Tenant.LegacyCompanyScopes
                .Where(scope => scope.IsActive)
                .Select(scope => scope.CompanyId)
                .Distinct()
                .Order()
                .ToArray(),
            membership.LegacyUserCode);
}
