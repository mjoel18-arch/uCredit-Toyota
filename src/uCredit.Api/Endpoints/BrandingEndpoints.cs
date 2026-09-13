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
            // Tenant resolution is intentionally isolated for future OIDC claims/host mapping.
            var tenantCode = context.Request.Headers["X-Tenant-Code"].FirstOrDefault();
            var theme = await provider.GetCurrentAsync(tenantCode, cancellationToken);
            return Results.Ok(theme);
        })
        .AllowAnonymous()
        .WithName("GetCurrentBranding")
        .WithTags("Branding");

        return endpoints;
    }
}

