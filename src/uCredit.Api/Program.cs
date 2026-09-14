using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
var isTesting = builder.Environment.IsEnvironment("Testing");
if (!isTesting)
{
    builder.Services.AddOptions<ExternalAuthenticationOptions>()
        .BindConfiguration(ExternalAuthenticationOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var authentication = builder.Configuration
                .GetSection(ExternalAuthenticationOptions.SectionName)
                .Get<ExternalAuthenticationOptions>()!;

            options.Authority = authentication.Authority;
            options.Audience = authentication.Audience;
            options.RequireHttpsMetadata = true;
            options.SaveToken = false;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(1),
            };
        });
}
else
{
    builder.Services.AddAuthentication();
}

builder.Services.AddTransient<IClaimsTransformation, ExternalIdRoleClaimsTransformation>();

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
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
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
