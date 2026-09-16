using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UCredit.Infrastructure.Identity.Models;
using UCredit.Infrastructure.Identity.Tenants;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Api.IntegrationTests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
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
            services.AddSingleton<ITenantMembershipStore, FakeTenantMembershipStore>();
            services.AddSingleton<IDeploymentTenantPolicy>(new DeploymentTenantPolicy("TENANT-A"));
            services.AddSingleton<FakeTenantCookieIssuer>();
            services.AddSingleton<ITenantCookieIssuer>(
                serviceProvider => serviceProvider.GetRequiredService<FakeTenantCookieIssuer>());
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

        var userId = TestIdentityData.GetUserId(mode);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "integration-test-user"),
            new(ClaimTypes.NameIdentifier, userId.ToString("D"))
        };
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

internal static class TestIdentityData
{
    public static readonly Guid SingleMembershipUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DeploymentTenantId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid OtherTenantId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid DeploymentPermissionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid MultipleMembershipUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid InactiveMembershipUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid InactiveTenantUserId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    public static readonly Guid InactiveUserId = Guid.Parse("00000000-0000-0000-0000-000000000005");
    public static readonly Guid ChangeTenantUserId = Guid.Parse("00000000-0000-0000-0000-000000000006");

    public static Guid GetUserId(string mode) => mode switch
    {
        "multiple-memberships" => MultipleMembershipUserId,
        "inactive-membership" => InactiveMembershipUserId,
        "inactive-tenant" => InactiveTenantUserId,
        "inactive-user" => InactiveUserId,
        "change-tenant" => ChangeTenantUserId,
        _ => SingleMembershipUserId
    };
}

internal sealed class FakeTenantMembershipStore : ITenantMembershipStore
{
    private static readonly ActiveTenantMembership TenantA = new(
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        "TENANT-A",
        "Tenant A",
        ["contracts.read"],
        [101]);

    private static readonly ActiveTenantMembership TenantB = new(
        Guid.Parse("10000000-0000-0000-0000-000000000002"),
        "TENANT-B",
        "Tenant B",
        ["contracts.write"],
        [202, 203]);

    private static readonly Dictionary<Guid, (bool IsActive, IReadOnlyList<ActiveTenantMembership> Active)> Users = new()
    {
        [TestIdentityData.SingleMembershipUserId] = (true, [TenantA]),
        [TestIdentityData.MultipleMembershipUserId] = (true, [TenantA, TenantB]),
        [TestIdentityData.InactiveMembershipUserId] = (true, []),
        [TestIdentityData.InactiveTenantUserId] = (true, []),
        [TestIdentityData.InactiveUserId] = (false, []),
        [TestIdentityData.ChangeTenantUserId] = (true, [TenantA, TenantB])
    };

    public Task<ApplicationUser?> GetActiveUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ApplicationUser?>(
            Users.TryGetValue(userId, out var data) && data.IsActive
                ? new ApplicationUser { Id = userId, UserName = "integration-test-user", IsActive = true }
                : null);
    }

    public Task<IReadOnlyList<ActiveTenantMembership>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Users.TryGetValue(userId, out var data) && data.IsActive
            ? data.Active
            : (IReadOnlyList<ActiveTenantMembership>)Array.Empty<ActiveTenantMembership>());
    }

    public Task<ActiveTenantMembership?> FindActiveMembershipAsync(
        Guid userId,
        string tenantCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var membership = Users.TryGetValue(userId, out var data) && data.IsActive
            ? data.Active.SingleOrDefault(candidate =>
                string.Equals(candidate.TenantCode, tenantCode.Trim(), StringComparison.OrdinalIgnoreCase))
            : null;
        return Task.FromResult(membership);
    }

    public Task<ActiveTenantMembership?> FindActiveMembershipAsync(
        Guid userId,
        Guid tenantId,
        string tenantCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var membership = Users.TryGetValue(userId, out var data) && data.IsActive
            ? data.Active.SingleOrDefault(candidate =>
                candidate.TenantId == tenantId &&
                string.Equals(candidate.TenantCode, tenantCode.Trim(), StringComparison.OrdinalIgnoreCase))
            : null;
        return Task.FromResult(membership);
    }
}

internal sealed class FakeTenantCookieIssuer : ITenantCookieIssuer
{
    private readonly Dictionary<Guid, IReadOnlyList<Claim>> issuedClaims = [];

    public Task<bool> IssueAsync(
        ClaimsPrincipal principal,
        ActiveTenantMembership membership,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Task.FromResult(false);
        }

        issuedClaims[userId] = TenantSelectionClaims.Build(membership);
        return Task.FromResult(true);
    }

    public IReadOnlyList<Claim> GetIssuedClaims(Guid userId) =>
        issuedClaims.TryGetValue(userId, out var claims) ? claims : [];
}
