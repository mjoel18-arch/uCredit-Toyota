using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using UCredit.Infrastructure.Identity.Models;

namespace UCredit.Infrastructure.Identity.Tenants;

public sealed class IdentityTenantCookieIssuer(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IDeploymentTenantPolicy deploymentTenantPolicy) : ITenantCookieIssuer
{
    public async Task<bool> IssueAsync(
        ClaimsPrincipal principal,
        ActiveTenantMembership membership,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.IsActive || !deploymentTenantPolicy.IsAllowed(membership.TenantCode))
        {
            return false;
        }

        var claims = TenantSelectionClaims.Build(membership);

        // SignInWithClaimsAsync creates a new principal from the factory, so claims from a previous tenant are not reused.
        await signInManager.SignInWithClaimsAsync(user, isPersistent: false, claims);
        return true;
    }
}
