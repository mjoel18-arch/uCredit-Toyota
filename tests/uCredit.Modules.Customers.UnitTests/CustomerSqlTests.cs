using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerSqlTests
{
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
}
