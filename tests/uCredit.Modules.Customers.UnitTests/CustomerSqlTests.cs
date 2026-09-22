using UCredit.Infrastructure.LegacySql;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerSqlTests
{
    [Theory]
    [InlineData(false, "Server=synthetic", true, "pr_t", LegacyWriteConfigurationReason.NotDevelopment)]
    [InlineData(true, "", true, "pr_t", LegacyWriteConfigurationReason.MissingWriteConnection)]
    [InlineData(true, "Server=synthetic", false, "pr_t", LegacyWriteConfigurationReason.WriteTestsNotAllowed)]
    [InlineData(true, "Server=synthetic", true, "", LegacyWriteConfigurationReason.MissingExpectedDatabase)]
    [InlineData(true, "Server=synthetic", true, "other", LegacyWriteConfigurationReason.InvalidExpectedDatabase)]
    public void WriteConfigurationReportsSafeReason(bool development, string connection, bool allow, string database, LegacyWriteConfigurationReason expected)
    {
        var options = new LegacySqlOptions { WriteConnectionString = connection, AllowLegacyWriteTests = allow, LegacyWriteTestDatabase = database };
        Assert.Equal(expected, LegacyCustomerWriteRepository.GetConfigurationReason(options, development));
    }

    [Fact]
    public void ValidWriteConfigurationPassesAllConfigurationGuards()
    {
        var options = new LegacySqlOptions { WriteConnectionString = "Server=synthetic", AllowLegacyWriteTests = true, LegacyWriteTestDatabase = "pr_t" };
        Assert.Null(LegacyCustomerWriteRepository.GetConfigurationReason(options, isDevelopment: true));
    }

    [Fact]
    public void SearchSqlUsesOnlyParameterizedCriteriaAndNoSelectStar()
    {
        var sql = LegacyCustomerReadRepository.CreateSearchSql(new CustomerSearchCriteria(PersonId: 42, Rfc: "ABC010203AB1", Name: "Cliente", LegalPersonalityCode: 1));
        Assert.Contains("@PersonId", sql, StringComparison.Ordinal);
        Assert.Contains("@Rfc", sql, StringComparison.Ordinal);
        Assert.Contains("@NamePrefix", sql, StringComparison.Ordinal);
        Assert.Contains("@LegalPersonalityCode", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("42", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ABC010203AB1", sql, StringComparison.Ordinal);
        Assert.Contains("PNA_FG_STATUS", sql, StringComparison.Ordinal);
        Assert.Contains("PAR_FL_CVE = 6", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void RelatedSqlAppliesApprovedPrimaryPhoneAndActiveEmailRules()
    {
        var phones = LegacyCustomerReadRepository.CreatePhonesSql();
        var emails = LegacyCustomerReadRepository.CreateEmailsSql();
        Assert.Contains("TFN_FG_STATUS = 1", phones, StringComparison.Ordinal);
        Assert.Contains("TFN_FG_REGDEFAULT DESC, TFN_FL_CVE ASC", phones, StringComparison.Ordinal);
        Assert.Contains("MAI_FG_STATUS = 1", emails, StringComparison.Ordinal);
        Assert.DoesNotContain("TOP (1)", emails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadinessSqlUsesActiveExistsChecksWithoutAccountSensitiveColumns()
    {
        var sql = LegacyCustomerProfileReadinessRepository.CreateSql();

        Assert.Contains("P.PNA_FL_PERSONA = @PersonId", sql, StringComparison.Ordinal);
        Assert.Contains("P.PNA_FG_STATUS = 1", sql, StringComparison.Ordinal);
        Assert.Contains("DMO_FG_STATUS = 1", sql, StringComparison.Ordinal);
        Assert.Contains("TFN_FG_STATUS = 1", sql, StringComparison.Ordinal);
        Assert.Contains("PCT_FG_STATUS = 1", sql, StringComparison.Ordinal);
        Assert.Equal(3, sql.Split("EXISTS", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("PCT_NO_CUENTA", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PCT_NO_CLABE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AllowedCompanyIds", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CustomerCreationDoesNotWriteOrReservePhoneOrAddress()
    {
        Assert.DoesNotContain("CDOMICILIO", LegacyCustomerWriteRepository.CreatePersonInsertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CTELEFONO", LegacyCustomerWriteRepository.CreatePersonInsertSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@RoleCode", LegacyCustomerWriteRepository.CreateRoleInsertSql, StringComparison.Ordinal);
        Assert.DoesNotContain("@Phone", LegacyCustomerWriteRepository.CreateRoleInsertSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TaxRegimeLookupUsesSatKeyStatusAndPersonalityWithoutInternalId()
    {
        var sql = LegacyCustomerWriteRepository.TaxRegimeLookupSql;
        Assert.Contains("RFI_CL_CLAVE = @TaxRegimeCode", sql, StringComparison.Ordinal);
        Assert.Contains("RFI_CL_PJURIDICA = @FiscalPersonality", sql, StringComparison.Ordinal);
        Assert.Contains("RFI_FG_STATUS = 1", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("RFI_FL_CVE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AddressManagementSqlUsesRouteKeysAndExpectedTimestamp()
    {
        Assert.Contains("DMO_FL_CVE = @AddressId", LegacyCustomerWriteRepository.AddressUpdateSql, StringComparison.Ordinal);
        Assert.Contains("PNA_FL_PERSONA = @PersonId", LegacyCustomerWriteRepository.AddressUpdateSql, StringComparison.Ordinal);
        Assert.Contains("DMO_FE_ULTMOD = @ExpectedModifiedAt", LegacyCustomerWriteRepository.AddressUpdateSql, StringComparison.Ordinal);
        Assert.Contains("@Billing", LegacyCustomerWriteRepository.AddressInsertSql, StringComparison.Ordinal);
        Assert.Contains("@Statements", LegacyCustomerWriteRepository.AddressInsertSql, StringComparison.Ordinal);
        Assert.Contains("@Other", LegacyCustomerWriteRepository.AddressInsertSql, StringComparison.Ordinal);
        Assert.DoesNotContain("PCT_NO_CUENTA", LegacyCustomerWriteRepository.AddressInsertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PCT_NO_CLABE", LegacyCustomerWriteRepository.AddressInsertSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddressReadSqlDoesNotExposeAccountDataAndUsesControlledTypeCatalog()
    {
        Assert.Contains("P.PAR_FL_CVE = 7", LegacyCustomerWriteRepository.AddressSelectSql, StringComparison.Ordinal);
        Assert.DoesNotContain("PCT_NO_CUENTA", LegacyCustomerAddressReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PCT_NO_CLABE", LegacyCustomerAddressReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT *", LegacyCustomerAddressReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyCustomerAddressReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyCustomerAddressReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacyCustomerAddressReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccountSqlIsParameterizedAndNeverWritesSensitiveValuesToLogs()
    {
        Assert.Contains("@AccountNumber", LegacyCustomerAccountWriteRepository.InsertSql, StringComparison.Ordinal);
        Assert.Contains("@Clabe", LegacyCustomerAccountWriteRepository.InsertSql, StringComparison.Ordinal);
        Assert.Contains("PCT_FE_ULTMOD = @ExpectedModifiedAt", LegacyCustomerAccountWriteRepository.UpdateSql, StringComparison.Ordinal);
        Assert.Contains("PCT_FG_STATUS = @Status", LegacyCustomerAccountWriteRepository.StateUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT *", LegacyCustomerAccountReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyCustomerAccountReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyCustomerAccountReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacyCustomerAccountReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PCT_NO_CUENTA", "Customer account mutation failed. ExceptionType={ExceptionType} SqlNumber={SqlNumber} Stage={Stage} CorrelationId={CorrelationId}", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BankCatalogSqlIsReadOnlyAndUsesOnlyApprovedColumns()
    {
        var sql = LegacyCustomerBankReadRepository.ReadSql;
        Assert.Contains("BCO_FL_CVE", sql, StringComparison.Ordinal);
        Assert.Contains("BCO_DS_NOMBRE", sql, StringComparison.Ordinal);
        Assert.Contains("BCO_FG_STATUS = 1", sql, StringComparison.Ordinal);
        Assert.Contains("BCO_FG_REAL = 1", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY BCO_DS_NOMBRE ASC, BCO_FL_CVE ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("PCT_NO_CUENTA", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PCT_NO_CLABE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersonRoleCatalogSqlIsActiveParameterizedAndReadOnly()
    {
        var sql = LegacyCustomerRoleCatalogRepository.ReadSql;
        Assert.Contains("PAR_FL_CVE = @CatalogCode", sql, StringComparison.Ordinal);
        Assert.Contains("PAR_FG_STATUS = 1", sql, StringComparison.Ordinal);
        Assert.Contains("PAR_CL_VALOR > 0", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY PAR_DS_DESCRIPCION ASC, PAR_CL_VALOR ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("1234567890", "123456789012345678")]
    public void AccountRulesAcceptSyntheticNumericValues(string accountNumber, string clabe)
    {
        Assert.Equal(accountNumber, CustomerAccountRules.RequiredSensitive(accountNumber, 20, "Account number"));
        Assert.Equal(clabe, CustomerAccountRules.RequiredClabe(clabe));
        CustomerAccountRules.ValidateBranch(0);
        CustomerAccountRules.ValidateBranch(short.MaxValue);
    }

    [Fact]
    public void AccountRulesRejectInvalidSensitiveValuesAndNegativeBranch()
    {
        Assert.Throws<CustomerAccountValidationException>(() => CustomerAccountRules.RequiredSensitive(" ", 20, "Account number"));
        Assert.Throws<CustomerAccountValidationException>(() => CustomerAccountRules.RequiredClabe("123"));
        Assert.Throws<CustomerAccountValidationException>(() => CustomerAccountRules.RequiredClabe("12345678901234567A"));
        Assert.Throws<CustomerAccountValidationException>(() => CustomerAccountRules.ValidateBranch(-1));
    }
}
