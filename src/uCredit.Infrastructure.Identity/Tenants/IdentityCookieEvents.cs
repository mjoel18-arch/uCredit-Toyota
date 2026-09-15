using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace UCredit.Infrastructure.Identity.Tenants;

public sealed class IdentityCookieEvents(ITenantMembershipStore membershipStore) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var cancellationToken = context.HttpContext.RequestAborted;
        if (context.Principal is not { } principal)
        {
            await RejectAsync(context);
            return;
        }
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await RejectAsync(context);
            return;
        }

        var user = await membershipStore.GetActiveUserAsync(userId, cancellationToken);
        if (user is null)
        {
            await RejectAsync(context);
            return;
        }

        var tenantIdClaims = principal.FindAll(TenantClaimTypes.Id).ToArray();
        var tenantCodeClaims = principal.FindAll(TenantClaimTypes.Code).ToArray();
        var permissionClaims = principal.FindAll("permission").ToArray();

        if (tenantIdClaims.Length == 0 && tenantCodeClaims.Length == 0)
        {
            if (permissionClaims.Length > 0 || permissionClaims.Any(claim => string.IsNullOrWhiteSpace(claim.Value)))
            {
                await RejectAsync(context);
            }

            // A freshly authenticated user may have no tenant selected, but then has no permissions.
            return;
        }

        if (tenantIdClaims.Length != 1 ||
            tenantCodeClaims.Length != 1 ||
            !Guid.TryParse(tenantIdClaims[0].Value, out var tenantId) ||
            string.IsNullOrWhiteSpace(tenantCodeClaims[0].Value) ||
            permissionClaims.Any(claim => string.IsNullOrWhiteSpace(claim.Value)))
        {
            await RejectAsync(context);
            return;
        }

        var membership = await membershipStore.FindActiveMembershipAsync(
            userId,
            tenantId,
            tenantCodeClaims[0].Value,
            cancellationToken);
        if (membership is null)
        {
            await RejectAsync(context);
            return;
        }

        if (!ClaimsMatch(permissionClaims, membership.PermissionCodes))
        {
            var replacement = CloneWithoutTenantSelection(principal);
            replacement.AddClaims(TenantSelectionClaims.Build(membership));
            context.ReplacePrincipal(new ClaimsPrincipal(replacement));
            context.ShouldRenew = true;
        }
    }

    private static ClaimsIdentity CloneWithoutTenantSelection(ClaimsPrincipal principal)
    {
        var source = principal.Identities.First();
        var identity = new ClaimsIdentity(source.AuthenticationType, source.NameClaimType, source.RoleClaimType);
        identity.AddClaims(source.Claims.Where(claim =>
            claim.Type is not TenantClaimTypes.Id and
            not TenantClaimTypes.Code and
            not "permission"));
        return identity;
    }

    private static bool ClaimsMatch(IEnumerable<Claim> claims, IEnumerable<string> permissions)
    {
        var claimArray = claims.ToArray();
        var actual = claimArray.Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
        var expected = permissions.ToHashSet(StringComparer.Ordinal);
        return actual.SetEquals(expected) && actual.Count == claimArray.Length;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}