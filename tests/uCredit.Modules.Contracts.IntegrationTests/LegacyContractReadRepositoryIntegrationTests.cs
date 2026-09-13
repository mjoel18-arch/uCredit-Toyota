using Microsoft.Extensions.Options;
using UCredit.Infrastructure.LegacySql;
using UCredit.Infrastructure.LegacySql.Contracts;

namespace UCredit.Modules.Contracts.IntegrationTests;

public sealed class LegacyContractReadRepositoryIntegrationTests
{
    [Fact(Skip = "Secure Legacy SQL test connection is not configured yet.")]
    public async Task GetByNumberAsyncQueriesConfiguredTestDatabase()
    {
        var options = Options.Create(new LegacySqlOptions
        {
            ReadConnectionString = string.Empty,
        });
        var repository = new LegacyContractReadRepository(options);

        var result = await repository.GetByNumberAsync("TEST-CONTRACT", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }
}
