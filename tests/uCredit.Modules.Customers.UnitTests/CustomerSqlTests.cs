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
    public void OptionalInteriorNumberUsesLegacyEmptyStringAndSchemaSize()
    {
        Assert.Equal(string.Empty, LegacyCustomerWriteRepository.NormalizeOptionalLegacyString(null, 100));
        Assert.Equal(string.Empty, LegacyCustomerWriteRepository.NormalizeOptionalLegacyString("   ", 100));
        Assert.Equal("12-B", LegacyCustomerWriteRepository.NormalizeOptionalLegacyString(" 12-B ", 100));
        Assert.Contains("@InteriorNumber", LegacyCustomerWriteRepository.CreateAddressInsertSql, StringComparison.Ordinal);
        Assert.Contains("PAI_FL_CVE", LegacyCustomerWriteRepository.CreateAddressInsertSql, StringComparison.Ordinal);
    }

    [Fact]
    public void AddressInsertHasEveryConfirmedRequiredColumnAndNoConcatenatedValues()
    {
        var sql = LegacyCustomerWriteRepository.CreateAddressInsertSql;
        foreach (var column in new[]
        {
            "DMO_FL_CVE", "PNA_FL_PERSONA", "DMO_CL_CPOSTAL", "DMO_DS_NUMEXT", "DMO_DS_NUMINT",
            "DMO_FG_TDIRECCION", "DMO_FG_STATUS", "DMO_FG_FACTURA", "DMO_FG_EDOCTA", "DMO_FG_REGDEFAULT",
            "DMO_FG_OTROS", "DMO_FE_ULTMOD", "PAI_FL_CVE"
        })
        {
            Assert.Contains(column, sql, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("VALUES ('", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@Rfc", sql, StringComparison.Ordinal);
        Assert.Contains("Stage=", "Customer creation failed. ExceptionType={ExceptionType} Stage={Stage} CorrelationId={CorrelationId}", StringComparison.Ordinal);
    }

    [Fact]
    public void AddressFailuresAreAssignedAddressStageByOperationBoundary()
    {
        Assert.Equal("address", LegacyCustomerWriteRepository.AddressStage);
        Assert.Contains("@InteriorNumber", LegacyCustomerWriteRepository.CreateAddressInsertSql, StringComparison.Ordinal);
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
}
