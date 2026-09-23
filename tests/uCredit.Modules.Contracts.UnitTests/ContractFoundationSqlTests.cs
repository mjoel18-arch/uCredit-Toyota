using UCredit.Infrastructure.LegacySql.Contracts;
using Microsoft.Extensions.Options;
using UCredit.Infrastructure.LegacySql;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Contracts.UnitTests;

public sealed class ContractFoundationSqlTests
{
    [Fact]
    public void CatalogQueriesAreReadOnlyAndParameterized()
    {
        var sql = string.Join("\n", LegacyContractFoundationRepository.OperationsSql,
            LegacyContractFoundationRepository.CnbvSql,
            LegacyContractFoundationRepository.CustomerSql,
            LegacyContractFoundationRepository.CfdiUsesSql,
            LegacyContractFoundationRepository.OrdinaryRateSql,
            LegacyContractFoundationRepository.LateRateSql,
            LegacyContractFoundationRepository.BusinessDateSql);

        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@CompanyIds", sql, StringComparison.Ordinal);
        Assert.Contains("@PersonId", sql, StringComparison.Ordinal);
        Assert.Contains("@OperationCode", sql, StringComparison.Ordinal);
        Assert.Contains("@CurrencyCode", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationsUseOnlyActiveNonProposalRowsForAuthorizedCompanies()
    {
        var sql = LegacyContractFoundationRepository.OperationsSql;

        Assert.Contains("TOP_FG_STATUS = 1", sql, StringComparison.Ordinal);
        Assert.Contains("TOP_FG_PP = 0", sql, StringComparison.Ordinal);
        Assert.Contains("EMP_FL_CVE IN @CompanyIds", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveCatalogQueriesReturnOnlySafeProjection()
    {
        var sql = string.Join("\n", LegacyContractFoundationRepository.CnbvSql,
            LegacyContractFoundationRepository.CfdiUsesSql,
            LegacyContractFoundationRepository.OrdinaryRateSql,
            LegacyContractFoundationRepository.LateRateSql);

        Assert.DoesNotContain("CTO_FL_CVE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PCT_NO_CUENTA", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PCT_NO_CLABE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LegacyUserCode", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContractAddressCatalogReusesTheCustomerAddressReadProjection()
    {
        var sql = UCredit.Infrastructure.LegacySql.Customers.LegacyCustomerAddressReadRepository.ReadSql;

        Assert.Contains("D.DMO_FL_CVE AS AddressId", sql, StringComparison.Ordinal);
        Assert.Contains("D.DMO_FG_STATUS AS IsActive", sql, StringComparison.Ordinal);
        Assert.Contains("@PersonId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ContractAddressCatalogProjectsOnlyActiveAddressesWithoutLegacyFields()
    {
        var addressReadRepository = new StubAddressReadRepository(
        [
            CreateAddress(7, true, "Dirección fiscal"),
            CreateAddress(8, false, "Dirección histórica"),
        ]);
        var repository = new LegacyContractFoundationRepository(
            Options.Create(new LegacySqlOptions()),
            addressReadRepository);

        var result = await repository.GetActiveAddressesAsync(42, TestContext.Current.CancellationToken);

        var address = Assert.Single(result!);
        Assert.Equal(7, address.Id);
        Assert.Equal(2, address.TypeCode);
        Assert.Equal("Dirección fiscal", address.TypeDescription);
        Assert.Equal(42, addressReadRepository.PersonId);
    }

    private static ManagedCustomerAddress CreateAddress(int addressId, bool isActive, string description) => new(
        addressId,
        42,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        2,
        description,
        [],
        isActive,
        addressId == 7,
        new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Unspecified),
        1);

    private sealed class StubAddressReadRepository(IReadOnlyList<ManagedCustomerAddress> addresses) : ICustomerAddressReadRepository
    {
        public int PersonId { get; private set; }

        public Task<IReadOnlyList<ManagedCustomerAddress>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
        {
            PersonId = personId;
            return Task.FromResult<IReadOnlyList<ManagedCustomerAddress>?>(addresses);
        }
    }
}
