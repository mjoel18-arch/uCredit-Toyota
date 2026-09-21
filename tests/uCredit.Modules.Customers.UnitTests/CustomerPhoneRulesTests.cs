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
        Assert.Contains("@PhoneId", LegacyCustomerPhoneWriteRepository.PhoneInsertSql, StringComparison.Ordinal);
        Assert.Contains("@ExpectedModifiedAt", LegacyCustomerPhoneWriteRepository.PhoneUpdateSql, StringComparison.Ordinal);
        Assert.Contains("TFN_FE_ULTMOD = @ExpectedModifiedAt", LegacyCustomerPhoneWriteRepository.PhoneUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT *", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacyCustomerPhoneReadRepository.ReadSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CTELEFONO", LegacyCustomerPhoneWriteRepository.PhoneInsertSql, StringComparison.Ordinal);
    }

    [Fact]
    public void MapsCompleteLegacyRowWithoutMaterializingTheModuleRecord()
    {
        var result = LegacyCustomerPhoneWriteRepository.Map(new LegacyCustomerPhoneWriteRepository.LegacyCustomerPhoneRow
        {
            PhoneId = 10,
            PersonId = 42,
            PhoneTypeCode = 3,
            AddressId = 7,
            AreaCode = " 00 ",
            PhoneNumber = " 0000000000 ",
            StatusCode = 1,
            IsDefault = 1,
            ModifiedAt = new DateTime(2025, 1, 2, 3, 4, 5, 678),
        });

        Assert.Equal(7, result.AddressId);
        Assert.Equal("00", result.AreaCode);
        Assert.Equal("0000000000", result.PhoneNumber);
        Assert.True(result.IsDefault);
        Assert.Equal(1, result.StatusCode);
        Assert.Equal(new DateTime(2025, 1, 2, 3, 4, 5, 678), result.ModifiedAt);
    }

    [Fact]
    public void NullableAddressIdFailsFastDuringExplicitMapping()
    {
        var row = new LegacyCustomerPhoneWriteRepository.LegacyCustomerPhoneRow
        {
            PhoneId = 10,
            PersonId = 42,
            PhoneTypeCode = 1,
            AddressId = null,
            ModifiedAt = new DateTime(2025, 1, 2),
        };

        Assert.Throws<InvalidOperationException>(() => LegacyCustomerPhoneWriteRepository.Map(row));
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
            AddressId = 7,
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
