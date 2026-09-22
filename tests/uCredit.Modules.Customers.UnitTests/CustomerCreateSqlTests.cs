using UCredit.Infrastructure.LegacySql.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerCreateSqlTests
{
    [Fact]
    public void CustomerCreateDoesNotWriteOrReserveEmailData()
    {
        Assert.DoesNotContain("CPERSONA_EMAIL", LegacyCustomerWriteRepository.CreatePersonInsertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("KEMAIL_USO", LegacyCustomerWriteRepository.CreatePersonInsertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PNA_DS_EMAIL", LegacyCustomerWriteRepository.CreatePersonInsertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@Email", LegacyCustomerWriteRepository.CreatePersonInsertSql, StringComparison.OrdinalIgnoreCase);
    }
}
