using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerSearchValidatorTests
{
    [Fact]
    public void RequiresAtLeastOnePrimaryCriteria()
    {
        var errors = CustomerSearchValidator.Validate(new CustomerSearchCriteria());
        Assert.Contains("criteria", errors.Keys);
    }

    [Fact]
    public void ValidatesRfcNamePageSizeAndLegalPersonality()
    {
        var errors = CustomerSearchValidator.Validate(new CustomerSearchCriteria(
            Rfc: new string('A', 14), Name: new string('B', 201), LegalPersonalityCode: 99, PageSize: 101));
        Assert.Contains("rfc", errors.Keys);
        Assert.Contains("name", errors.Keys);
        Assert.Contains("legalPersonality", errors.Keys);
        Assert.Contains("pageSize", errors.Keys);
    }

    [Fact]
    public void AcceptsConfirmedLegalPersonalities()
    {
        foreach (var code in new[] { 1, 2, 20 })
        {
            Assert.DoesNotContain("legalPersonality", CustomerSearchValidator.Validate(new CustomerSearchCriteria(PersonId: 1, LegalPersonalityCode: code)).Keys);
        }
    }

    [Fact]
    public void SelectsLowestIdAmongMultipleActiveDefaults()
    {
        var phones = new[]
        {
            new CustomerPhone(8, null, "8", null, true),
            new CustomerPhone(3, null, "3", null, true),
            new CustomerPhone(1, null, "1", null, false),
        };

        Assert.Equal(3, CustomerPhoneSelector.SelectPrimary(phones)!.PhoneId);
    }

    [Fact]
    public void ReturnsNullWhenNoActiveDefaultExists()
    {
        var phones = new[] { new CustomerPhone(1, null, "1", null, false) };
        Assert.Null(CustomerPhoneSelector.SelectPrimary(phones));
    }

    [Fact]
    public void IgnoresInactivePhonesBeforeSelectingPrimary()
    {
        var phones = new[] { new CustomerPhone(1, null, "1", null, true) };
        var activePhones = Array.Empty<CustomerPhone>();
        Assert.Null(CustomerPhoneSelector.SelectPrimary(activePhones));
    }
}
