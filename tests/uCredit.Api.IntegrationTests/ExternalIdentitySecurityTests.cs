using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UCredit.Api.Security;

namespace UCredit.Api.IntegrationTests;

public sealed class ExternalIdentitySecurityTests(TestApiFactory factory)
    : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task ContractsReadRoleIsTransformedToPermission()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("roles", "contracts.read")],
            "test"));

        var transformed = await new ExternalIdRoleClaimsTransformation().TransformAsync(principal);

        Assert.Contains(transformed.Claims, claim => claim.Type == "permission" && claim.Value == "contracts.read");
    }

    [Fact]
    public async Task OtherRolesDoNotGrantContractsRead()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("roles", "contracts.write")],
            "test"));

        var transformed = await new ExternalIdRoleClaimsTransformation().TransformAsync(principal);

        Assert.DoesNotContain(transformed.Claims, claim => claim.Type == "permission" && claim.Value == "contracts.read");
    }

    [Fact]
    public async Task ExistingPermissionIsNotDuplicated()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("roles", "contracts.read"),
                new Claim("permission", "contracts.read"),
            ],
            "test"));

        var transformed = await new ExternalIdRoleClaimsTransformation().TransformAsync(principal);

        Assert.Equal(1, transformed.Claims.Count(claim => claim.Type == "permission" && claim.Value == "contracts.read"));
    }

    [Fact]
    public void ProductionContractsReadPolicyRemainsPermissionClaim()
    {
        var options = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var policy = options.GetPolicy("contracts.read");

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy.Requirements.OfType<ClaimsAuthorizationRequirement>());
        Assert.Equal("permission", requirement.ClaimType);
        Assert.Contains("contracts.read", requirement.AllowedValues!);
    }

    [Fact]
    public void MissingAuthenticationConfigurationFailsValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<ExternalAuthenticationOptions>()
            .Bind(new ConfigurationBuilder().AddInMemoryCollection().Build().GetSection(ExternalAuthenticationOptions.SectionName))
            .ValidateDataAnnotations();
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ExternalAuthenticationOptions>>().Value);

        Assert.Contains("Authority", exception.Message, StringComparison.Ordinal);
    }
}