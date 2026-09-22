using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerGeneralManagementTests
{
    [Fact]
    public void GeneralReadSqlDoesNotExposeSensitiveExecutionFields()
    {
        var sql = LegacyCustomerGeneralRepository.GeneralSelectSql + LegacyCustomerGeneralRepository.RolesSelectSql;

        Assert.Contains("PNA_FE_ULTMOD", sql, StringComparison.Ordinal);
        Assert.Contains("PFI_FE_ULTMOD", sql, StringComparison.Ordinal);
        Assert.Contains("PMO_FE_ULTMOD", sql, StringComparison.Ordinal);
        Assert.Contains("PAR_FL_CVE = 5", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyUserCode", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USR_CL_CVE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneralWriteSqlUsesExpectedTokensAndActivityFive()
    {
        Assert.Contains("PNA_FE_ULTMOD = @OperationDate", LegacyCustomerGeneralRepository.PersonUpdateSql, StringComparison.Ordinal);
        Assert.Contains("PNA_FE_ULTMOD = @ExpectedPersonModifiedAt", LegacyCustomerGeneralRepository.PersonUpdateSql, StringComparison.Ordinal);
        Assert.Contains("PFI_FE_ULTMOD = @ExpectedSubtypeModifiedAt", LegacyCustomerGeneralRepository.PhysicalUpdateSql, StringComparison.Ordinal);
        Assert.Contains("PMO_FE_ULTMOD = @ExpectedSubtypeModifiedAt", LegacyCustomerGeneralRepository.MoralUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("ATV_FL_CVE, BIT_FE_OPERACION", LegacyCustomerGeneralRepository.PersonUpdateSql, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneralRulesTrimOptionalValuesAndRejectFutureDates()
    {
        Assert.Equal("Contacto", CustomerGeneralRules.Optional("  Contacto ", 200, "contactName"));
        Assert.Equal(string.Empty, CustomerGeneralRules.Optional("   ", 200, "contactName"));
        Assert.Throws<CustomerGeneralValidationException>(() => CustomerGeneralRules.Optional(new string('x', 201), 200, "contactName"));
        Assert.Throws<CustomerGeneralValidationException>(() => CustomerGeneralRules.ValidateDate(DateTime.UtcNow.AddDays(1), "birthDate"));
    }

    [Fact]
    public void CustomerCreationRejectsInsurerRole()
    {
        var command = new CustomerCreateCommand(
            CustomerCreatePersonality.Individual,
            "AAA010101AAA",
            "Nombre",
            "Apellido",
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-30)),
            1,
            1,
            1,
            1,
            "605",
            [10]);

        var errors = CustomerCreateValidator.Validate(command);

        Assert.Contains("roleCodes", errors.Keys);
    }
}
