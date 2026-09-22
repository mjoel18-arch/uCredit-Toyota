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
using UCredit.Infrastructure.LegacySql.Customers;

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
            services.RemoveAll<ICustomerPhoneReadRepository>();
            services.AddSingleton<ICustomerPhoneReadRepository, FakeCustomerPhoneReadRepository>();
            services.RemoveAll<ICustomerAccountReadRepository>();
            services.AddSingleton<ICustomerAccountReadRepository, FakeCustomerAccountReadRepository>();
            services.RemoveAll<ICustomerBankReadRepository>();
            services.AddSingleton<ICustomerBankReadRepository, FakeCustomerBankReadRepository>();
            services.RemoveAll<ICustomerRoleCatalogRepository>();
            services.AddSingleton<ICustomerRoleCatalogRepository, FakeCustomerRoleCatalogRepository>();
            services.RemoveAll<ICustomerEmailReadRepository>();
            services.AddSingleton<ICustomerEmailReadRepository, FakeCustomerEmailReadRepository>();
            services.RemoveAll<ICustomerEmailUsageRepository>();
            services.AddSingleton<ICustomerEmailUsageRepository, FakeCustomerEmailUsageRepository>();
            services.RemoveAll<ICustomerEmailWriteRepository>();
            services.AddSingleton<ICustomerEmailWriteRepository, FakeCustomerEmailWriteRepository>();
            services.RemoveAll<ICustomerGeneralRepository>();
            services.AddSingleton<ICustomerGeneralRepository, FakeCustomerGeneralRepository>();
            services.AddSingleton<IPersonPepChecker, FakePersonPepChecker>();
            services.RemoveAll<ICustomerAccountWriteRepository>();
            services.AddSingleton<ICustomerAccountWriteRepository, FakeCustomerAccountWriteRepository>();
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

        if (string.Equals(mode, "customers-write", StringComparison.Ordinal))
        {
            claims.Add(new Claim("permission", "contracts.read"));
            claims.Add(new Claim("permission", "customers.read"));
            claims.Add(new Claim("permission", "customers.write"));
            claims.Add(new Claim(TenantClaimTypes.Id, TestIdentityData.DeploymentTenantId.ToString("D")));
            claims.Add(new Claim(TenantClaimTypes.Code, "TENANT-A"));
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

internal sealed class FakeCustomerPhoneReadRepository : ICustomerPhoneReadRepository
{
    public Task<IReadOnlyList<ManagedCustomerPhone>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ManagedCustomerPhone>?>(personId == 42
            ? [
                new ManagedCustomerPhone(2, 42, 3, null, "00", "0000000000", null, 1, null, true, new DateTime(2025, 1, 1), null),
                new ManagedCustomerPhone(3, 42, 2, null, "00", "0000000001", "10", 0, "Histórico", false, new DateTime(2025, 1, 2), null)
            ]
            : null);
    }
}

internal sealed class FakeCustomerAccountReadRepository : ICustomerAccountReadRepository
{
    private static readonly ManagedCustomerAccount First = new(7001, 42, 2, "Banco de prueba", 12, 1, "Pesos", 1, "Cheques", null, 1, "••••0001", "••••0002", new DateTime(2025, 1, 1));
    private static readonly ManagedCustomerAccount Second = new(7002, 42, 3, "Banco alterno", 13, 1, "Pesos", 2, "Tarjeta de credito", null, 2, "••••0003", "••••0004", new DateTime(2025, 1, 2));

    public Task<IReadOnlyList<ManagedCustomerAccount>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ManagedCustomerAccount>?>(personId switch
        {
            42 => [First, Second],
            47 => [],
            999 => null,
            _ => [],
        });
    }
}

internal sealed class FakeCustomerBankReadRepository : ICustomerBankReadRepository
{
    public Task<IReadOnlyList<CustomerBank>> GetActiveRealAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CustomerBank>>([new(2, "Banco Alfa"), new(4, "Banco Beta")]);
}

internal sealed class FakeCustomerRoleCatalogRepository : ICustomerRoleCatalogRepository
{
    public Task<IReadOnlyList<CustomerRoleOption>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CustomerRoleOption>>([
            new(25, "ACCIONISTA"),
            new(3, "APODERADO"),
            new(1, "CLIENTE")
        ]);
}

internal sealed class FakeCustomerGeneralRepository : ICustomerGeneralRepository
{
    private static readonly DateTime PersonModifiedAt = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SubtypeModifiedAt = new(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Dictionary<int, CustomerGeneralProfile> Profiles = new()
    {
        [42] = Profile(42, 1, [new(1, "CLIENTE", true, true), new(25, "ACCIONISTA", true, true)]),
        [53] = Profile(53, 2, [new(3, "APODERADO", true, true)]),
        [54] = Profile(54, 20, [new(1, "CLIENTE", true, true)]),
        [55] = Profile(55, 1, [new(25, "ACCIONISTA", true, true), new(2, "ROL HISTORICO", false, false)]),
        [56] = Profile(56, 1, [new(1, "CLIENTE", true, true)]),
        [57] = Profile(57, 1, [new(1, "CLIENTE", true, true)]),
        [58] = Profile(58, 1, [new(1, "CLIENTE", true, true)]),
        [59] = Profile(59, 1, [new(1, "CLIENTE", true, true)]),
        [60] = Profile(60, 1, [new(10, "ASEGURADORA", true, false)]),
        [61] = Profile(61, 1, [new(1, "CLIENTE", true, true)], "PEP-MATCH"),
        [62] = Profile(62, 1, [new(1, "CLIENTE", true, true)], "PEP-UNAVAILABLE")
    };

    public Task<CustomerGeneralProfile?> GetAsync(int personId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Profiles.TryGetValue(personId, out var profile) ? profile : null);
    }

    public Task<CustomerGeneralProfile> UpdateAsync(CustomerGeneralUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default)
    {
        if (command.PersonId == 56) throw new CustomerGeneralConflictException("customer_modified", "Modified.");
        if (command.PersonId == 57) throw new CustomerGeneralConflictException("customer_modified", "Modified.");
        if (command.PersonId == 58) throw new LegacyWriteNotConfiguredException("Not configured.", "configuration", LegacyWriteConfigurationReason.MissingWriteConnection);
        if (!Profiles.TryGetValue(command.PersonId, out var current)) throw new CustomerGeneralNotFoundException("Not found.");
        if (command.RoleCodes is { Count: 0 }) throw new CustomerGeneralValidationException("At least one role is required.");
        if (command.RoleCodes is not null && command.RoleCodes.Distinct().Count() != command.RoleCodes.Count)
            throw new CustomerGeneralValidationException("Roles must be distinct.");
        if (command.RoleCodes is not null && command.RoleCodes.Any(code => code is not (1 or 3 or 25 or 10)))
            throw new CustomerGeneralValidationException("Role is inactive or unknown.");
        if (current.Roles.Any(role => role.Code == 10 && role.IsActive) && command.RoleCodes is not null && !command.RoleCodes.Contains(10))
            throw new CustomerGeneralConflictException("customer_role_required", "The insurer role cannot be removed.");
        if (command.RoleCodes is not null && command.RoleCodes.Contains(10) && !current.Roles.Any(role => role.Code == 10 && role.IsActive))
            throw new CustomerGeneralValidationException("The insurer role cannot be added from this screen.");
        var roles = command.RoleCodes is null ? current.Roles : command.RoleCodes.Select(code => new CustomerGeneralRole(code, code switch { 1 => "CLIENTE", 3 => "APODERADO", 25 => "ACCIONISTA", 10 => "ASEGURADORA", _ => null }, true, code != 10)).ToArray();
        var result = current with { FirstName = command.FirstName, PaternalSurname = command.PaternalSurname, MaternalSurname = command.MaternalSurname, BirthDate = command.BirthDate, LegalName = command.LegalName, ContactName = command.ContactName, ContactPosition = command.ContactPosition, Roles = roles };
        Profiles[command.PersonId] = result;
        return Task.FromResult(result);
    }

    private static CustomerGeneralProfile Profile(int personId, int personality, IReadOnlyList<CustomerGeneralRole> roles, string rfc = "ABC010203AB1") =>
        new(personId, personality, rfc, personality == 20 ? null : "Nombre", personality == 20 ? null : "Apellido", null, new DateTime(1980, 1, 1), personality == 20 ? "Sociedad" : null, personality == 20 ? "Contacto" : null, personality == 20 ? "Puesto" : null, 1, PersonModifiedAt, SubtypeModifiedAt, roles);
}

internal sealed class FakePersonPepChecker : IPersonPepChecker
{
    public Task<PepCheckResult> CheckAsync(CustomerCreateCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(command.Rfc switch
        {
            "PEP-MATCH" => new PepCheckResult(PepCheckStatus.Match),
            "PEP-UNAVAILABLE" => new PepCheckResult(PepCheckStatus.Unavailable),
            _ => new PepCheckResult(PepCheckStatus.NoMatch),
        });
    }
}

internal sealed class FakeCustomerAccountWriteRepository : ICustomerAccountWriteRepository
{
    private static ManagedCustomerAccount Result(int personId, int accountId, byte status = 1) =>
        new(accountId, personId, 2, "Banco de prueba", 12, 1, "Pesos", 1, "Cheques", null, status, "••••0001", "••••0002", new DateTime(2025, 1, 3));

    public Task<ManagedCustomerAccount> CreateAsync(CustomerAccountCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default)
    {
        return command.PersonId switch
        {
            48 => Task.FromException<ManagedCustomerAccount>(new CustomerAccountConflictException("account_duplicate", "Duplicate account.")),
            50 => Task.FromException<ManagedCustomerAccount>(new LegacyWriteNotConfiguredException("Not configured.", "configuration", LegacyWriteConfigurationReason.MissingWriteConnection)),
            _ => Task.FromResult(Result(command.PersonId, 8001)),
        };
    }

    public Task<ManagedCustomerAccount> UpdateAsync(CustomerAccountUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        command.PersonId == 49
            ? Task.FromException<ManagedCustomerAccount>(new CustomerAccountConflictException("account_modified", "Modified."))
            : Task.FromResult(Result(command.PersonId, command.AccountId));

    public Task<ManagedCustomerAccount> ActivateAsync(CustomerAccountStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(Result(command.PersonId, command.AccountId));

    public Task<ManagedCustomerAccount> DeactivateAsync(CustomerAccountStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(Result(command.PersonId, command.AccountId, 2));
}

internal sealed class FakeCustomerEmailReadRepository : ICustomerEmailReadRepository
{
    private static readonly ManagedCustomerEmail First = new(8001, 42, "Contacto sintético", "uno@example.invalid", 1, [1, 2], new DateTime(2025, 1, 1));
    private static readonly ManagedCustomerEmail Second = new(8002, 42, null, "dos@example.invalid", 2, [3], new DateTime(2025, 1, 2));

    public Task<IReadOnlyList<ManagedCustomerEmail>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ManagedCustomerEmail>?>(personId switch
        {
            42 => [First, Second],
            47 => [],
            999 => null,
            _ => [],
        });
}

internal sealed class FakeCustomerEmailUsageRepository : ICustomerEmailUsageRepository
{
    public Task<IReadOnlyList<CustomerEmailUsage>> GetActiveUsagesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CustomerEmailUsage>>([
            new(1, "Envío de facturas"),
            new(2, "Envío de estado de cuenta"),
            new(3, "Salesforce")
        ]);
}

internal sealed class FakeCustomerEmailWriteRepository : ICustomerEmailWriteRepository
{
    private static ManagedCustomerEmail Result(int personId, int emailId, string email, int status = 1, IReadOnlyList<int>? usages = null) =>
        new(emailId, personId, "Contacto sintético", email, status, usages ?? [1], new DateTime(2025, 1, 3));

    private static void ValidateUsages(IReadOnlyList<int> usageCodes)
    {
        var normalized = CustomerEmailRules.NormalizeUsages(usageCodes);
        if (normalized.Any(code => code is < 1 or > 3))
            throw new CustomerEmailValidationException("One or more email uses are invalid.");
    }

    public Task<ManagedCustomerEmail> CreateAsync(CustomerEmailCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default)
    {
        CustomerEmailRules.NormalizeEmail(command.Email);
        ValidateUsages(command.UsageCodes);
        return command.PersonId switch
        {
            48 => Task.FromException<ManagedCustomerEmail>(new CustomerEmailConflictException("email_duplicate", "Duplicate email.")),
            50 => Task.FromException<ManagedCustomerEmail>(new LegacyWriteNotConfiguredException("Not configured.", "configuration", LegacyWriteConfigurationReason.MissingWriteConnection)),
            51 => Task.FromException<ManagedCustomerEmail>(new CustomerEmailValidationException("The email cannot be registered.")),
            999 => Task.FromException<ManagedCustomerEmail>(new CustomerEmailNotFoundException("Customer was not found.")),
            _ => Task.FromResult(Result(command.PersonId, 8003, command.Email, usages: command.UsageCodes)),
        };
    }

    public Task<ManagedCustomerEmail> UpdateAsync(CustomerEmailUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default)
    {
        if (command.Email is not null) CustomerEmailRules.NormalizeEmail(command.Email);
        ValidateUsages(command.UsageCodes);
        return command.PersonId switch
        {
            48 => Task.FromException<ManagedCustomerEmail>(new CustomerEmailConflictException("email_duplicate", "Duplicate email.")),
            49 => Task.FromException<ManagedCustomerEmail>(new CustomerEmailConflictException("email_modified", "Modified.")),
            50 => Task.FromException<ManagedCustomerEmail>(new LegacyWriteNotConfiguredException("Not configured.", "configuration", LegacyWriteConfigurationReason.MissingWriteConnection)),
            999 => Task.FromException<ManagedCustomerEmail>(new CustomerEmailNotFoundException("Email was not found.")),
            _ => Task.FromResult(Result(command.PersonId, command.EmailId, command.Email ?? "conservado@example.invalid", usages: command.UsageCodes)),
        };
    }

    public Task<ManagedCustomerEmail> ActivateAsync(CustomerEmailStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        command.PersonId == 48
            ? Task.FromException<ManagedCustomerEmail>(new CustomerEmailConflictException("email_duplicate", "Duplicate email."))
            : command.PersonId == 49
                ? Task.FromException<ManagedCustomerEmail>(new CustomerEmailConflictException("email_modified", "Modified."))
                : command.PersonId == 999
                    ? Task.FromException<ManagedCustomerEmail>(new CustomerEmailNotFoundException("Email was not found."))
                    : Task.FromResult(Result(command.PersonId, command.EmailId, "activado@example.invalid"));

    public Task<ManagedCustomerEmail> DeactivateAsync(CustomerEmailStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        command.PersonId == 999
            ? Task.FromException<ManagedCustomerEmail>(new CustomerEmailNotFoundException("Email was not found."))
            : Task.FromResult(Result(command.PersonId, command.EmailId, "desactivado@example.invalid", 2, [1, 2]));
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
        TestIdentityData.DeploymentTenantId,
        "TENANT-A",
        "Tenant A",
        ["contracts.read"],
        [101],
        "TESTUSR");

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
