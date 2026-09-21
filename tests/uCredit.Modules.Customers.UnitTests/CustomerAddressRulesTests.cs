using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerAddressRulesTests
{
    [Fact]
    public void UniqueAddressDerivesAllUses()
    {
        var flags = CustomerAddressRules.ToLegacyFlags(1, ["billing", "statements", "other"]);

        Assert.Equal((1, 1, 1), flags);
    }

    [Fact]
    public void FiscalAddressRequiresBillingAndAllowsOptionalUses()
    {
        var flags = CustomerAddressRules.ToLegacyFlags(2, ["BILLING", "statements", "statements"]);

        Assert.Equal((1, 1, 0), flags);
    }

    [Fact]
    public void AdministrativeAndSocialAddressesRejectBilling()
    {
        Assert.Throws<CustomerAddressValidationException>(() => CustomerAddressRules.ToLegacyFlags(3, ["billing"]));
        Assert.Throws<CustomerAddressValidationException>(() => CustomerAddressRules.ToLegacyFlags(4, ["billing"]));
    }

    [Fact]
    public void UnknownUsesAreRejected()
    {
        Assert.Throws<CustomerAddressValidationException>(() => CustomerAddressRules.ToLegacyFlags(2, ["unknown"]));
    }

    [Fact]
    public void EmptyUsesAreAllowedOnlyForAdministrativeAndSocialAddresses()
    {
        Assert.Equal((0, 0, 0), CustomerAddressRules.ToLegacyFlags(3, []));
        Assert.Equal((0, 0, 0), CustomerAddressRules.ToLegacyFlags(4, []));
        Assert.Throws<CustomerAddressValidationException>(() => CustomerAddressRules.ToLegacyFlags(1, []));
        Assert.Throws<CustomerAddressValidationException>(() => CustomerAddressRules.ToLegacyFlags(2, []));
    }
}
