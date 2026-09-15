using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UCredit.Api.Security;
using UCredit.Infrastructure.Identity.Models;
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
        group.MapGet("/me", Me)
            .RequireAuthorization()
            .WithMetadata(new RequestSizeLimitAttribute(2 * 1024));
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

    private static IResult Me(ClaimsPrincipal principal) =>
        Results.Ok(new
        {
            userName = principal.Identity?.Name,
            permissions = principal.FindAll("permission").Select(c => c.Value).Distinct().Order().ToArray()
        });
}
public sealed record LoginRequest(string UserName, string Password);
