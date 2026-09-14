using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Api.IntegrationTests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ => { });

            services.RemoveAll<IContractReadRepository>();
            services.AddSingleton<IContractReadRepository, FakeContractReadRepository>();
        });
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestAuthentication";
    public const string HeaderName = "X-Test-Auth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var mode = Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(mode))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, "integration-test-user") };
        if (string.Equals(mode, "with-permission", StringComparison.Ordinal))
        {
            claims.Add(new Claim("permission", "contracts.read"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

internal sealed class FakeContractReadRepository : IContractReadRepository
{
    private static readonly ContractSummary KnownContract = new(
        "CONTRACT-1",
        42,
        "Integration Test Customer",
        "LOAN",
        "Loan",
        null,
        1000m,
        750m,
        null,
        null,
        null,
        1,
        "Active",
        null,
        null);

    public Task<PagedResult<ContractSummary>> SearchAsync(
        ContractSearchCriteria criteria,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<ContractSummary>(
            [KnownContract],
            criteria.Page,
            criteria.PageSize,
            1));

    public Task<ContractSummary?> GetByNumberAsync(
        string contractNumber,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ContractSummary?>(
            string.Equals(contractNumber, KnownContract.ContractNumber, StringComparison.Ordinal)
                ? KnownContract
                : null);
}