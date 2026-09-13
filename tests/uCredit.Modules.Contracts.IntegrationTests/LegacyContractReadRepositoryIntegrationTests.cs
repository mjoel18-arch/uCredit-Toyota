using Microsoft.Extensions.Options;
using UCredit.Infrastructure.LegacySql;
using UCredit.Infrastructure.LegacySql.Contracts;

namespace UCredit.Modules.Contracts.IntegrationTests;

public sealed class LegacyContractReadRepositoryIntegrationTests
{
    public static bool HasSecureReadConnection =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LegacySql__ReadConnectionString"));

    [Fact(SkipUnless = nameof(HasSecureReadConnection), Skip = "Set LegacySql__ReadConnectionString from a secure test configuration source.")]
    public async Task SearchAsyncQueriesConfiguredTestDatabase()
    {
        var connectionString = Environment.GetEnvironmentVariable("LegacySql__ReadConnectionString");
        var options = Options.Create(new LegacySqlOptions
        {
            ReadConnectionString = connectionString!,
        });
        var repository = new LegacyContractReadRepository(options);
        var criteria = new UCredit.Modules.Contracts.Contracts.ContractSearchCriteria(
            "TEST-CONTRACT", null, null, null, null, null, null, null, PageSize: 10);

        var result = await repository.SearchAsync(criteria, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }
}
