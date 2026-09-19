using System.Security.Claims;
using UCredit.Application.Execution;
using UCredit.Infrastructure.Identity.Tenants;

namespace UCredit.Api.Security;

public sealed class HttpExecutionActorContext(
    IHttpContextAccessor accessor,
    ITenantMembershipStore membershipStore) : IExecutionActorContext
{
    public async Task<ExecutionActor> GetAsync(CancellationToken cancellationToken = default)
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(principal.FindFirstValue(TenantClaimTypes.Id), out var tenantId))
        {
            return new ExecutionActor(null);
        }

        var tenantCode = principal.FindFirstValue(TenantClaimTypes.Code);
        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            return new ExecutionActor(null);
        }

        var membership = await membershipStore.FindActiveMembershipAsync(userId, tenantId, tenantCode, cancellationToken);
        return new ExecutionActor(membership?.LegacyUserCode);
    }
}
