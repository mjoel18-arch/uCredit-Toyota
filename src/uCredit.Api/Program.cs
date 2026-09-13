using Microsoft.AspNetCore.Authentication;
using System.Text.Json.Serialization;
using UCredit.Api.Endpoints;
using UCredit.Api.Middleware;
using UCredit.Api.Security;
using UCredit.Infrastructure.LegacySql;
using UCredit.Modules.Branding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddAuthentication();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
            DevelopmentAuthenticationHandler.SchemeName,
            _ => { });
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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    name = "uCredit API",
    status = "skeleton",
    version = "0.1.0"
}));
app.MapHealthChecks("/health");
app.MapBrandingEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapContractEndpoints();
}
else
{
    app.MapMethods(
        "/api/v1/contracts/{**path}",
        ["GET", "POST", "PUT", "PATCH", "DELETE"],
        () => Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Identity provider is not configured."));
}

app.Run();

public partial class Program;
