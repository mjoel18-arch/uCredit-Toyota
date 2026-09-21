using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UCredit.Infrastructure.Identity.Models;
using UCredit.Infrastructure.Identity.Tenants;
using UCredit.Application.Execution;
using UCredit.Modules.Contracts.Contracts;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.IntegrationTests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    internal const string DeploymentTenantCode = "TENANT-A";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Deployment:TenantCode", DeploymentTenantCode);
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
            services.AddSingleton<IContractAmortizationReadRepository, FakeContractAmortizationReadRepository>();
            services.RemoveAll<ICustomerReadRepository>();
            services.AddSingleton<ICustomerReadRepository, FakeCustomerReadRepository>();
            services.RemoveAll<ICustomerProfileReadinessRepository>();
            services.AddSingleton<ICustomerProfileReadinessRepository, FakeCustomerProfileReadinessRepository>();
            services.RemoveAll<IExecutionTenantContext>();
            services.AddScoped<IExecutionTenantContext, FakeExecutionTenantContext>();
            services.AddSingleton<ITenantMembershipStore, FakeTenantMembershipStore>();
            services.AddSingleton<IDeploymentTenantPolicy>(new DeploymentTenantPolicy(DeploymentTenantCode));
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
        if (string.Equals(mode, "with-permission", StringComparison.Ordinal) ||
            string.Equals(mode, "customers-with-permission", StringComparison.Ordinal))
        {
            claims.Add(new Claim("permission", "contracts.read"));
        }

        if (string.Equals(mode, "customers-with-permission", StringComparison.Ordinal) ||
            string.Equals(mode, "customers-without-tenant", StringComparison.Ordinal))
        {
            claims.Add(new Claim("permission", "customers.read"));
        }

        if (string.Equals(mode, "customers-with-permission", StringComparison.Ordinal))
        {
            claims.Add(new Claim(TenantClaimTypes.Id, TestIdentityData.DeploymentTenantId.ToString("D")));
            claims.Add(new Claim(TenantClaimTypes.Code, "TENANT-A"));
        }

        if (string.Equals(mode, "toyota", StringComparison.Ordinal))
        {
            claims.Add(new Claim(TenantClaimTypes.Code, "TOYOTA"));
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
        "MXN",
        "PESO MEXICANO",
        48,
        60,
        new DateOnly(2025, 12, 15),
        new DateOnly(2025, 12, 20),
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

internal sealed class FakeCustomerReadRepository : ICustomerReadRepository
{
    private static readonly Customer KnownCustomer = new(
        42,
        "ABC010203AB1",
        "Cliente de prueba",
        1,
        "FISICA",
        1,
        "ACTIVO",
        new CustomerAddress(10, "01000", "CIUDAD DE MÉXICO", "ÁLVARO OBREGÓN", "CIUDAD DE MÉXICO", "SAN ÁNGEL", "AV. PRUEBA", "1", null, 1, "DIRECCION UNICA"),
        new CustomerPhone(2, "55", "5555555555", null, true),
        [new CustomerRole(1, "CLIENTE")],
        [new CustomerPhone(2, "55", "5555555555", null, true), new CustomerPhone(3, "55", "5555555556", "10", false)],
        [new CustomerEmail(4, "Contacto", "cliente@example.test")]);

    public Task<PagedCustomers> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var matches = criteria.PersonId is 42 || string.Equals(criteria.Rfc, KnownCustomer.Rfc, StringComparison.OrdinalIgnoreCase) ||
            (criteria.Name is not null && KnownCustomer.Name.StartsWith(criteria.Name, StringComparison.OrdinalIgnoreCase))
            ? new[] { KnownCustomer }
            : Array.Empty<Customer>();
        return Task.FromResult(new PagedCustomers(matches, criteria.Page, criteria.PageSize, matches.Length));
    }

    public Task<Customer?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Customer?>(personId == KnownCustomer.PersonId ? KnownCustomer : null);
    }
}

internal sealed class FakeCustomerProfileReadinessRepository : ICustomerProfileReadinessRepository
{
    public Task<CustomerProfileReadiness?> GetAsync(int personId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<CustomerProfileReadiness?>(personId switch
        {
            42 => new CustomerProfileReadiness(42, true, true, true, true),
            43 => new CustomerProfileReadiness(43, true, false, true, true),
            44 => new CustomerProfileReadiness(44, true, true, false, true),
            45 => new CustomerProfileReadiness(45, true, true, true, false),
            46 => new CustomerProfileReadiness(46, true, false, false, false),
            _ => null,
        });
    }
}

internal sealed class FakeExecutionTenantContext(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : IExecutionTenantContext
{
    public Task<ExecutionTenant?> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var principal = httpContextAccessor.HttpContext?.User;
        var tenantId = principal?.FindFirstValue(TenantClaimTypes.Id);
        var selectedTenantCode = principal?.FindFirstValue(TenantClaimTypes.Code);
        var tenantCode = configuration["Deployment:TenantCode"]
            ?? throw new InvalidOperationException(
                "Deployment:TenantCode must be configured for integration tests.");
        return Task.FromResult<ExecutionTenant?>(
            Guid.TryParse(tenantId, out var parsedId) &&
            string.Equals(selectedTenantCode, tenantCode, StringComparison.Ordinal)
                ? new ExecutionTenant(parsedId, tenantCode, [101])
                : null);
    }
}

internal sealed class FakeContractAmortizationReadRepository : IContractAmortizationReadRepository
{
    private static readonly ContractAmortizationSchedule KnownSchedule = new(
        "CONTRACT-1",
        1,
        4,
        new ContractAmortizationPayment(
            100,
            0,
            4,
            ContractAmortizationPaymentStatus.Generated,
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 1, 31),
            new DateOnly(2025, 1, 1),
            1000m,
            1000m,
            1000m,
            0m,
            160m,
            1000m,
            1160m,
            1160m),
        [
            new ContractAmortizationPayment(
                101,
                1,
                4,
                ContractAmortizationPaymentStatus.Generated,
                new DateOnly(2025, 2, 1),
                new DateOnly(2025, 2, 28),
                new DateOnly(2025, 2, 1),
                10000m,
                9000m,
                1000m,
                500m,
                80m,
                1500m,
                1580m,
                1580m),
            new ContractAmortizationPayment(
                102,
                2,
                4,
                ContractAmortizationPaymentStatus.Pending,
                new DateOnly(2025, 3, 1),
                new DateOnly(2025, 3, 31),
                new DateOnly(2025, 3, 1),
                9000m,
                8000m,
                1000m,
                400m,
                64m,
                1400m,
                1464m,
                1464m)
        ]);

    public Task<ContractAmortizationSchedule?> GetScheduleByNumberAsync(
        string contractNumber,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ContractAmortizationSchedule?>(
            contractNumber switch
            {
                "CONTRACT-1" => KnownSchedule,
                "NODOWNPAY-001" => KnownSchedule with
                {
                    DownPayment = null,
                },
                "NOAMORT-001" => null,
                _ => null,
            });
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
