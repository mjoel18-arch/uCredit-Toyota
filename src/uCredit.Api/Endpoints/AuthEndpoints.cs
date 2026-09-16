using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UCredit.Api.Security;
using UCredit.Infrastructure.Identity.Models;
using UCredit.Infrastructure.Identity.Tenants;

namespace UCredit.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");
        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { requestToken = tokens.RequestToken, headerName = tokens.HeaderName });
        }).AllowAnonymous();
        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireRateLimiting("auth-login")
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));
        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .WithMetadata(new RequestSizeLimitAttribute(2 * 1024));
        group.MapGet("/me", MeAsync)
            .RequireAuthorization()
            .WithMetadata(new RequestSizeLimitAttribute(2 * 1024));
        group.MapPost("/select-tenant", SelectTenantAsync)
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireRateLimiting("tenant-select")
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        [FromServices] UserManager<ApplicationUser> userManager,
        [FromServices] SignInManager<ApplicationUser> signInManager)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.UserName.Length > 256 ||
            request.Password.Length > 256)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Username and password are required and must not exceed 256 characters."]
            });
        }

        var user = await userManager.FindByNameAsync(request.UserName.Trim());
        if (user is null || !user.IsActive)
            return Results.Unauthorized();

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
            return Results.Ok(new { authenticated = true });
        if (result.IsLockedOut)
            return Results.Problem(statusCode: StatusCodes.Status423Locked, title: "Account is temporarily locked.");
        return Results.Unauthorized();
    }

    private static async Task<IResult> LogoutAsync([FromServices] SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(
        ClaimsPrincipal principal,
        [FromServices] ITenantMembershipStore membershipStore,
        [FromServices] IDeploymentTenantPolicy deploymentTenantPolicy,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await membershipStore.GetActiveUserAsync(userId, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var memberships = (await membershipStore.GetActiveMembershipsAsync(userId, cancellationToken))
            .Where(membership => deploymentTenantPolicy.IsAllowed(membership.TenantCode))
            .ToArray();
        var selectedTenant = ReadSelectedTenant(principal, deploymentTenantPolicy);
        return Results.Ok(new
        {
            userName = user.UserName ?? user.Email ?? principal.Identity?.Name,
            tenant = selectedTenant,
            permissions = selectedTenant is null ? Array.Empty<string>() : ReadPermissionCodes(principal),
            memberships = memberships.Select(membership => new
            {
                tenantId = membership.TenantId,
                tenantCode = membership.TenantCode,
                tenantName = membership.TenantName,
                permissions = membership.PermissionCodes
            })
        });
    }

    private static async Task<IResult> SelectTenantAsync(
        ClaimsPrincipal principal,
        SelectTenantRequest request,
        [FromServices] ITenantMembershipStore membershipStore,
        [FromServices] ITenantCookieIssuer cookieIssuer,
        [FromServices] IDeploymentTenantPolicy deploymentTenantPolicy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantCode) || request.TenantCode.Length > 64)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantCode"] = ["Tenant code is required and must not exceed 64 characters."]
            });
        }

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await membershipStore.GetActiveUserAsync(userId, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var membership = await membershipStore.FindActiveMembershipAsync(
            userId,
            request.TenantCode,
            cancellationToken);
        if (membership is null ||
            !deploymentTenantPolicy.IsAllowed(membership.TenantCode) ||
            !await cookieIssuer.IssueAsync(principal, membership, cancellationToken))
        {
            // Use one response for unknown, inactive, foreign, and non-deployed tenants to avoid enumeration.
            return Results.Forbid();
        }

        return Results.Ok(new
        {
            tenantId = membership.TenantId,
            tenantCode = membership.TenantCode,
            permissions = membership.PermissionCodes
        });
    }

    private static object? ReadSelectedTenant(
        ClaimsPrincipal principal,
        IDeploymentTenantPolicy deploymentTenantPolicy)
    {
        var tenantId = principal.FindFirstValue(TenantClaimTypes.Id);
        var tenantCode = principal.FindFirstValue(TenantClaimTypes.Code);
        return tenantId is not null &&
            tenantCode is not null &&
            deploymentTenantPolicy.IsAllowed(tenantCode)
            ? new { tenantId, tenantCode }
            : null;
    }

    private static string[] ReadPermissionCodes(ClaimsPrincipal principal) =>
        principal.FindAll("permission")
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
}

public sealed record LoginRequest(string UserName, string Password);
public sealed record SelectTenantRequest(string TenantCode);
