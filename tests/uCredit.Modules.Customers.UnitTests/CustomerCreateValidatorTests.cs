using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerCreateValidatorTests
{
    [Fact]
    public void RejectsUnsupportedAddressTypesAndDuplicateUsages()
    {
        var command = Valid() with { AddressTypeCode = 7, EmailUsageCodes = [1, 1] };
        var errors = CustomerCreateValidator.Validate(command);
        Assert.Contains("addressTypeCode", errors.Keys);
        Assert.Contains("emailUsageCodes", errors.Keys);
    }

    [Fact]
    public void RequiresPersonFieldsByPersonality()
    {
        var errors = CustomerCreateValidator.Validate(Valid() with { FirstName = null, PaternalSurname = null, MaternalSurname = null });
        Assert.Contains("firstName", errors.Keys);
        Assert.Contains("paternalSurname", errors.Keys);
    }

    [Fact]
    public void AcceptsPhysicalBusinessMoralAndConfirmedEmailUses()
    {
        foreach (var personality in new[] { CustomerCreatePersonality.Individual, CustomerCreatePersonality.IndividualBusiness, CustomerCreatePersonality.Moral })
        {
            var command = Valid() with
            {
                LegalPersonality = personality,
                LegalName = personality == CustomerCreatePersonality.Moral ? "Synthetic Company" : null,
                CapitalRegime = personality == CustomerCreatePersonality.Moral ? "Synthetic Capital" : null
            };
            Assert.Empty(CustomerCreateValidator.Validate(command));
        }
    }

    private static CustomerCreateCommand Valid() => new(
        CustomerCreatePersonality.Individual, "AAA010101AAA", "Synthetic", "Person", "Test", null, null,
        new DateOnly(1980, 1, 1), 1, 1, 1, 1, 1, 1, "00000", "State", "City", "Municipality", "Neighborhood", "Street", "1", null, null, null, 1,
        1, "55", "5555555555", null, null, "Test Contact", "test@example.invalid", [1, 2]);
}
