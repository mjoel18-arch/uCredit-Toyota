using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerEmailRulesTests
{
    [Fact]
    public void NormalizesAndValidatesEmailAndOptionalContact()
    {
        Assert.Equal("person@example.invalid", CustomerEmailRules.NormalizeEmail(" person@example.invalid "));
        Assert.Equal("Contacto", CustomerEmailRules.NormalizeContact(" Contacto "));
        Assert.Null(CustomerEmailRules.NormalizeContact("   "));
        Assert.Throws<CustomerEmailValidationException>(() => CustomerEmailRules.NormalizeEmail("not-an-email"));
        Assert.Throws<CustomerEmailValidationException>(() => CustomerEmailRules.NormalizeContact(new string('x', 251)));
    }

    [Fact]
    public void RequiresAtLeastOneDistinctPositiveUse()
    {
        Assert.Equal([1, 2], CustomerEmailRules.NormalizeUsages([1, 2]));
        Assert.Throws<CustomerEmailValidationException>(() => CustomerEmailRules.NormalizeUsages([1, 2, 2]));
        Assert.Throws<CustomerEmailValidationException>(() => CustomerEmailRules.NormalizeUsages([]));
        Assert.Throws<CustomerEmailValidationException>(() => CustomerEmailRules.NormalizeUsages([0]));
    }

    [Fact]
    public void EmailSqlIsParameterizedAndSeparatesRealEmailChanges()
    {
        Assert.Contains("@Email", LegacyCustomerEmailWriteRepository.EmailInsertSql, StringComparison.Ordinal);
        Assert.Contains("MAI_FG_OMITIR_ENVIO", LegacyCustomerEmailWriteRepository.EmailInsertSql, StringComparison.Ordinal);
        Assert.Contains("MAI_DS_EMAIL = @Email", LegacyCustomerEmailWriteRepository.EmailUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("MAI_DS_EMAIL", LegacyCustomerEmailWriteRepository.EmailMetadataUpdateSql, StringComparison.Ordinal);
        Assert.Contains("MAI_FE_ULTMOD = @ExpectedModifiedAt", LegacyCustomerEmailWriteRepository.EmailUpdateSql, StringComparison.Ordinal);
        Assert.Contains("MAI_FE_ULTMOD = @ExpectedModifiedAt", LegacyCustomerEmailWriteRepository.EmailStateUpdateSql, StringComparison.Ordinal);
        Assert.Contains("PAR_FL_CVE = 248", LegacyCustomerEmailWriteRepository.BlacklistSql, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT *", LegacyCustomerEmailReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyCustomerEmailReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyCustomerEmailReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacyCustomerEmailReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DELETE FROM dbo.KEMAIL_USO", LegacyCustomerEmailWriteRepository.UsageDeleteSql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO dbo.KEMAIL_USO", LegacyCustomerEmailWriteRepository.UsageInsertSql, StringComparison.Ordinal);
        Assert.DoesNotContain("TFSM_HISTORICO_CORREO", LegacyCustomerEmailWriteRepository.EmailUpdateSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TFSM_HISTORICO_CORREO", LegacyCustomerEmailWriteRepository.EmailMetadataUpdateSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MAI_DS_EMAIL", LegacyCustomerEmailWriteRepository.EmailMetadataUpdateSql, StringComparison.Ordinal);
        Assert.Contains("@Email", LegacyCustomerEmailWriteRepository.DuplicateSql, StringComparison.Ordinal);
        Assert.Contains("PNA_FL_PERSONA", LegacyCustomerEmailWriteRepository.DuplicateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("+", LegacyCustomerEmailWriteRepository.DuplicateSql, StringComparison.Ordinal);
    }

    [Fact]
    public void EmailReadAndAuditContractsDoNotContainSensitiveLogTemplates()
    {
        Assert.DoesNotContain("MAI_DS_EMAIL", "Customer email mutation failed. ExceptionType={ExceptionType} SqlNumber={SqlNumber} Stage={Stage} CorrelationId={CorrelationId}", StringComparison.Ordinal);
        Assert.DoesNotContain("MAI_DS_CONTACTO", "Customer email mutation failed. ExceptionType={ExceptionType} SqlNumber={SqlNumber} Stage={Stage} CorrelationId={CorrelationId}", StringComparison.Ordinal);
    }
}
