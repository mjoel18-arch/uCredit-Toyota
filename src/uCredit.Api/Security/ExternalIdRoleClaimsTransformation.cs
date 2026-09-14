using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace UCredit.Api.Security;

public sealed class ExternalIdRoleClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim("permission", "contracts.read"))
        {
            return Task.FromResult(principal);
        }

        if (!principal.Claims.Any(claim =>
                (claim.Type == "roles" || claim.Type == ClaimTypes.Role) &&
                claim.Value == "contracts.read"))
        {
            return Task.FromResult(principal);
        }

        var identity = new ClaimsIdentity("ExternalIdRoleClaimsTransformation");
        identity.AddClaim(new Claim("permission", "contracts.read"));
        principal.AddIdentity(identity);
        return Task.FromResult(principal);
    }
}