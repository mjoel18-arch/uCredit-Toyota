using UCredit.Infrastructure.Identity.Tenants;
using UCredit.Modules.Branding;

namespace UCredit.Api.Endpoints;

public static class BrandingEndpoints
{
    public static IEndpointRouteBuilder MapBrandingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/branding/current", async (
            HttpContext context,
            IBrandThemeProvider provider,
            CancellationToken cancellationToken) =>
        {
            // The signed Identity cookie is the only tenant authority. Browser headers are ignored.
            var tenantCode = context.User.FindFirst(TenantClaimTypes.Code)?.Value;
            var theme = await provider.GetCurrentAsync(tenantCode, cancellationToken);
            return Results.Ok(theme);
        })
        .AllowAnonymous()
        .WithName("GetCurrentBranding")
        .WithTags("Branding");

        return endpoints;
    }
}

