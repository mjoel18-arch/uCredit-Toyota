using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql;
using UCredit.Infrastructure.LegacySql.Contracts;

namespace UCredit.Modules.Contracts.IntegrationTests;

public sealed class LegacyContractReadRepositoryIntegrationTests
{
    public static bool HasReadConnection => HasValue("LegacySql__ReadConnectionString");
    public static bool HasCompanyScope => HasValue("UCREDIT_TEST_COMPANY_IDS");

    public static bool HasContractTestData => HasReadConnection && HasCompanyScope && HasValue("UCREDIT_TEST_CONTRACT");
    public static bool HasRfcTestData => HasReadConnection && HasCompanyScope && HasValue("UCREDIT_TEST_RFC");
    public static bool HasPersonNamePrefixTestData => HasReadConnection && HasCompanyScope && HasValue("UCREDIT_TEST_PERSON_NAME_PREFIX");
    public static bool HasVinTestData => HasReadConnection && HasCompanyScope && HasValue("UCREDIT_TEST_VIN");

    [Fact(SkipUnless = nameof(HasContractTestData), Skip = "Set LegacySql__ReadConnectionString, UCREDIT_TEST_COMPANY_IDS and UCREDIT_TEST_CONTRACT from secure test configuration.")]
    public async Task GetByNumberAsyncReturnsExactConfiguredContract()
    {
        var contractNumber = GetRequiredValue("UCREDIT_TEST_CONTRACT");
        var result = await CreateRepository().GetByNumberAsync(contractNumber, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(contractNumber, result.ContractNumber);
    }

    [Fact(SkipUnless = nameof(HasRfcTestData), Skip = "Set LegacySql__ReadConnectionString, UCREDIT_TEST_COMPANY_IDS and UCREDIT_TEST_RFC from secure test configuration.")]
    public async Task SearchAsyncFindsContractsByExactRfc()
    {
        var result = await CreateRepository().SearchAsync(
            CreateCriteria(rfc: GetRequiredValue("UCREDIT_TEST_RFC")),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(result.Items);
    }

    [Fact(SkipUnless = nameof(HasPersonNamePrefixTestData), Skip = "Set LegacySql__ReadConnectionString, UCREDIT_TEST_COMPANY_IDS and UCREDIT_TEST_PERSON_NAME_PREFIX from secure test configuration.")]
    public async Task SearchAsyncFindsContractsByPersonNamePrefix()
    {
        var prefix = GetRequiredValue("UCREDIT_TEST_PERSON_NAME_PREFIX");
        var result = await CreateRepository().SearchAsync(CreateCriteria(personName: prefix), TestContext.Current.CancellationToken);

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.StartsWith(prefix, item.PersonName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact(SkipUnless = nameof(HasVinTestData), Skip = "Set LegacySql__ReadConnectionString, UCREDIT_TEST_COMPANY_IDS and UCREDIT_TEST_VIN from secure test configuration.")]
    public async Task SearchAsyncFindsContractsByExactVin()
    {
        var result = await CreateRepository().SearchAsync(
            CreateCriteria(vin: GetRequiredValue("UCREDIT_TEST_VIN")),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(result.Items);
    }

    [Fact(SkipUnless = nameof(HasPersonNamePrefixTestData), Skip = "Set LegacySql__ReadConnectionString, UCREDIT_TEST_COMPANY_IDS and UCREDIT_TEST_PERSON_NAME_PREFIX from secure test configuration.")]
    public async Task SearchAsyncReturnsRequestedPage()
    {
        var result = await CreateRepository().SearchAsync(
            CreateCriteria(personName: GetRequiredValue("UCREDIT_TEST_PERSON_NAME_PREFIX"), pageSize: 10),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.InRange(result.Items.Count, 0, 10);
        Assert.True(result.Total >= result.Items.Count);
    }

    [Fact(SkipUnless = nameof(HasVinTestData), Skip = "Set LegacySql__ReadConnectionString, UCREDIT_TEST_COMPANY_IDS and UCREDIT_TEST_VIN from secure test configuration.")]
    public async Task SearchAsyncDoesNotReturnDuplicateContractsForVin()
    {
        var result = await CreateRepository().SearchAsync(
            CreateCriteria(vin: GetRequiredValue("UCREDIT_TEST_VIN"), pageSize: 100),
            TestContext.Current.CancellationToken);
        var contractNumbers = result.Items.Select(item => item.ContractNumber);

        Assert.Equal(contractNumbers.Count(), contractNumbers.Distinct(StringComparer.Ordinal).Count());
    }

    private static LegacyContractReadRepository CreateRepository()
    {
        var options = Options.Create(new LegacySqlOptions
        {
            ReadConnectionString = GetRequiredValue("LegacySql__ReadConnectionString"),
        });

        return new LegacyContractReadRepository(
            options,
            new ConfiguredExecutionTenantContext(ParseCompanyIds()));
    }

    private static int[] ParseCompanyIds() =>
        GetRequiredValue("UCREDIT_TEST_COMPANY_IDS")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .ToArray();

    private static UCredit.Modules.Contracts.Contracts.ContractSearchCriteria CreateCriteria(
        string? rfc = null,
        string? personName = null,
        string? vin = null,
        int page = 1,
        int pageSize = 20) =>
        new(null, null, rfc, personName, vin, null, null, page, pageSize);

    private static bool HasValue(string variableName) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variableName));

    private static string GetRequiredValue(string variableName) =>
        Environment.GetEnvironmentVariable(variableName)
        ?? throw new InvalidOperationException($"Required test variable '{variableName}' is not configured.");

    private sealed class ConfiguredExecutionTenantContext(IReadOnlyList<int> companyIds) : IExecutionTenantContext
    {
        public Task<ExecutionTenant?> GetAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ExecutionTenant?>(
                new ExecutionTenant(Guid.Empty, "integration-test", companyIds));
        }
    }
}
