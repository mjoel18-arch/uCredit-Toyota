using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerPhoneRulesTests
{
    [Fact]
    public void NormalizesOptionalValuesAndPreservesPhysicalLimits()
    {
        Assert.Null(CustomerPhoneRules.NormalizeOptional("   ", 10, "areaCode"));
        Assert.Equal("55", CustomerPhoneRules.NormalizeOptional(" 55 ", 10, "areaCode"));
        Assert.Throws<CustomerPhoneValidationException>(() => CustomerPhoneRules.NormalizeOptional("12345678901", 10, "areaCode"));
        Assert.Equal("123", CustomerPhoneRules.NormalizeRequired(" 123 ", 20, "phoneNumber"));
    }

    [Fact]
    public void RejectsMissingPhoneTypeAndNumber()
    {
        Assert.Throws<CustomerPhoneValidationException>(() => CustomerPhoneRules.ValidateType(0));
        Assert.Throws<CustomerPhoneValidationException>(() => CustomerPhoneRules.NormalizeRequired(" ", 20, "phoneNumber"));
    }

    [Fact]
    public void ReadAndWriteSqlAreParameterizedAndReadOnlyReadSql()
    {
        Assert.Contains("@PersonId", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.Ordinal);
        Assert.Contains("DMO_FL_CVE", LegacyCustomerPhoneWriteRepository.PhoneInsertSql, StringComparison.Ordinal);
        Assert.Contains("@UnassociatedAddressId", LegacyCustomerPhoneWriteRepository.PhoneInsertSql, StringComparison.Ordinal);
        Assert.DoesNotContain("@NullAddressId", LegacyCustomerPhoneWriteRepository.PhoneInsertSql, StringComparison.Ordinal);
        Assert.DoesNotContain("@AddressId", LegacyCustomerPhoneWriteRepository.PhoneInsertSql, StringComparison.Ordinal);
        Assert.Contains("@ExpectedModifiedAt", LegacyCustomerPhoneWriteRepository.PhoneUpdateSql, StringComparison.Ordinal);
        Assert.Contains("TFN_FE_ULTMOD = @ExpectedModifiedAt", LegacyCustomerPhoneWriteRepository.PhoneUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("DMO_FL_CVE", LegacyCustomerPhoneWriteRepository.PhoneUpdateSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@AddressId", LegacyCustomerPhoneWriteRepository.PhoneUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT *", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DMO_FL_CVE", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CDOMICILIO", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CDOMICILIO", LegacyCustomerPhoneWriteRepository.PhoneUpdateSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CTELEFONO", LegacyCustomerPhoneWriteRepository.PhoneInsertSql, StringComparison.Ordinal);
    }

    [Fact]
    public void InsertUsesZeroForUnassociatedAddressWithTypedIntParameter()
    {
        var parameters = LegacyCustomerPhoneWriteRepository.PhoneParameters(
            personId: 42,
            phoneId: 10,
            new CustomerPhoneCreateCommand(42, 3, null, "55", "123", null, null, false),
            isDefault: true,
            operationDate: new DateTime(2025, 1, 2),
            legacyUserCode: "TEST");
        parameters.Add("UnassociatedAddressId", 0, System.Data.DbType.Int32);

        var parameter = parameters.GetType()
            .GetField("parameters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(parameters) as System.Collections.IDictionary;
        Assert.NotNull(parameter);
        var unassociatedAddress = parameter!["UnassociatedAddressId"];
        Assert.Equal(0, unassociatedAddress?.GetType().GetProperty("Value")?.GetValue(unassociatedAddress));
        Assert.Equal(System.Data.DbType.Int32, unassociatedAddress?.GetType().GetProperty("DbType")?.GetValue(unassociatedAddress));
    }

    [Fact]
    public void MapsCompleteLegacyRowWithoutMaterializingTheModuleRecord()
    {
        var result = LegacyCustomerPhoneWriteRepository.Map(new LegacyCustomerPhoneWriteRepository.LegacyCustomerPhoneRow
        {
            PhoneId = 10,
            PersonId = 42,
            PhoneTypeCode = 3,
            AreaCode = " 00 ",
            PhoneNumber = " 0000000000 ",
            StatusCode = 1,
            IsDefault = 1,
            ModifiedAt = new DateTime(2025, 1, 2, 3, 4, 5, 678),
        });

        Assert.Equal("00", result.AreaCode);
        Assert.Equal("0000000000", result.PhoneNumber);
        Assert.True(result.IsDefault);
        Assert.Equal(1, result.StatusCode);
        Assert.Equal(new DateTime(2025, 1, 2, 3, 4, 5, 678), result.ModifiedAt);
    }

    [Fact]
    public void NullableLegacyAddressAssociationIsIgnoredDuringExplicitMapping()
    {
        var row = new LegacyCustomerPhoneWriteRepository.LegacyCustomerPhoneRow
        {
            PhoneId = 10,
            PersonId = 42,
            PhoneTypeCode = 1,
            ModifiedAt = new DateTime(2025, 1, 2),
        };

        var result = LegacyCustomerPhoneWriteRepository.Map(row);
        Assert.Equal(10, result.PhoneId);
    }

    [Theory]
    [InlineData((byte)0, false, "InheritedInactive")]
    [InlineData((byte)1, true, "Active")]
    [InlineData((byte)2, false, "Inactive")]
    public void MapsLegacyStatesAndNullableOptionalValues(byte status, bool isDefault, string expectedStatus)
    {
        var result = LegacyCustomerPhoneWriteRepository.Map(new LegacyCustomerPhoneWriteRepository.LegacyCustomerPhoneRow
        {
            PhoneId = 10,
            PersonId = 42,
            PhoneTypeCode = 1,
            StatusCode = status,
            IsDefault = isDefault ? (byte)1 : (byte)0,
            ModifiedAt = new DateTime(2025, 1, 2),
            LongDistanceCode = " ",
            Extension = null,
            ContactName = null,
        });

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(isDefault, result.IsDefault);
        Assert.Null(result.LongDistanceCode);
        Assert.Null(result.Extension);
        Assert.Null(result.ContactName);
    }
}
