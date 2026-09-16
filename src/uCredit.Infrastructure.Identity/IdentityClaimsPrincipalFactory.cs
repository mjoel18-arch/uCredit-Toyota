using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using UCredit.Infrastructure.Identity.Models;
namespace UCredit.Infrastructure.Identity;
public sealed class IdentityClaimsPrincipalFactory : IUserClaimsPrincipalFactory<ApplicationUser>
{
    public Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id.ToString()));
        // Tenant resolution is intentionally pending; never aggregate permissions across tenants.
        return Task.FromResult(new ClaimsPrincipal(identity));
    }
}
