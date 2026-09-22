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

    [Fact]
    public void CustomerCreateWritesOnlySelectedRolesAndNotPhones()
    {
        Assert.Contains("@RoleCode", LegacyCustomerWriteRepository.CreateRoleInsertSql, StringComparison.Ordinal);
        Assert.DoesNotContain("CTELEFONO", LegacyCustomerWriteRepository.CreateRoleInsertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VALUES (@PersonId, 1", LegacyCustomerWriteRepository.CreateRoleInsertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CTELEFONO", LegacyCustomerWriteRepository.CreatePersonInsertSql, StringComparison.OrdinalIgnoreCase);
    }
}
