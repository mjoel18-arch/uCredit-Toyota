using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace UCredit.Api.Security;

public sealed class ExternalIdRoleClaimsTransformation : IClaimsTransformation
{
    private static readonly string[] SupportedPermissions = ["contracts.read", "customers.read"];

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var roleClaims = principal.Claims
            .Where(claim => claim.Type is "roles" or ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);
        var existingPermissions = principal.FindAll("permission")
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);
        var permissions = SupportedPermissions
            .Where(permission => roleClaims.Contains(permission) && !existingPermissions.Contains(permission))
            .ToArray();
        if (permissions.Length == 0) return Task.FromResult(principal);

        var identity = new ClaimsIdentity("ExternalIdRoleClaimsTransformation");
        foreach (var permission in permissions) identity.AddClaim(new Claim("permission", permission));
        principal.AddIdentity(identity);
        return Task.FromResult(principal);
    }
}
