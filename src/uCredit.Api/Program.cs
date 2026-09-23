using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UCredit.Api.Endpoints;
using UCredit.Api.Middleware;
using UCredit.Infrastructure.Identity;
using UCredit.Infrastructure.LegacySql;
using UCredit.Modules.Branding;
using UCredit.Modules.Customers.Customers;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");
var requirePepCheck = builder.Configuration.GetValue("CustomerCreation:RequirePepCheck", true);
if (builder.Environment.IsProduction() && !requirePepCheck)
    throw new InvalidOperationException("CustomerCreation__RequirePepCheck must be true in Production.");
builder.Services.AddOptions<CustomerCreationOptions>()
    .Bind(builder.Configuration.GetSection("CustomerCreation"));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UCredit.Application.Execution.IExecutionActorContext, UCredit.Api.Security.HttpExecutionActorContext>();
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
    options.AddPolicy("tenant-select", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
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
    options.AddPolicy("customers.read", policy =>
        policy.RequireClaim("permission", "customers.read"));
    options.AddPolicy("customers.write", policy =>
        policy.RequireClaim("permission", "customers.write"));
});
builder.Services.AddSingleton<IBrandThemeProvider, InMemoryBrandThemeProvider>();
builder.Services.AddLegacySql(builder.Configuration);

var app = builder.Build();
var legacyOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<LegacySqlOptions>>().Value;
var isDevelopment = app.Environment.IsDevelopment();
var writeConnectionConfigured = !string.IsNullOrWhiteSpace(legacyOptions.WriteConnectionString);
var expectedDatabaseConfigured = !string.IsNullOrWhiteSpace(legacyOptions.LegacyWriteTestDatabase);
UCredit.Api.StartupConfigurationLogging.Log(
    app.Logger,
    isDevelopment,
    writeConnectionConfigured,
    legacyOptions.AllowLegacyWriteTests,
    expectedDatabaseConfigured,
    requirePepCheck);
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
app.MapContractFoundationEndpoints();
app.MapCustomerEndpoints();
app.MapCustomerAddressEndpoints();
app.MapCustomerPhoneEndpoints();
app.MapCustomerAccountEndpoints();
app.MapCustomerBankEndpoints();
app.MapCustomerRoleCatalogEndpoints();
app.MapCustomerGeneralEndpoints();
app.MapCustomerEmailEndpoints();
app.MapCustomerCreateEndpoints();
app.Run();

public partial class Program;
