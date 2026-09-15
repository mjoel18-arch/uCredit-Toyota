using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UCredit.Api.Endpoints;
using UCredit.Api.Middleware;
using UCredit.Infrastructure.Identity;
using UCredit.Infrastructure.LegacySql;
using UCredit.Modules.Branding;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "uCredit.Csrf";
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isTesting ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

if (isTesting)
{
    builder.Services.AddAuthentication();
}
else
{
    builder.Services.AddIdentityInfrastructure(builder.Configuration, builder.Environment.EnvironmentName);
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("contracts.read", policy =>
        policy.RequireClaim("permission", "contracts.read"));
});
builder.Services.AddSingleton<IBrandThemeProvider, InMemoryBrandThemeProvider>();
builder.Services.AddLegacySql(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    name = "uCredit API",
    status = "skeleton",
    version = "0.1.0"
}));
app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapBrandingEndpoints();
app.MapContractEndpoints();
app.Run();

public partial class Program;
