using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerCreateValidatorTests
{
    [Fact]
    public void RejectsDuplicateEmailUsagesWithoutRequiringAddressData()
    {
        var command = Valid() with { EmailUsageCodes = [1, 1] };
        var errors = CustomerCreateValidator.Validate(command);
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
    public void AcceptsPhysicalPersonalitiesAndConfirmedEmailUses()
    {
        foreach (var personality in new[] { CustomerCreatePersonality.Individual, CustomerCreatePersonality.IndividualBusiness })
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

    [Fact]
    public void RejectsNonNumericTaxRegimeWithoutSendingItToSql()
    {
        var errors = CustomerCreateValidator.Validate(Valid() with { TaxRegimeCode = "not-a-sat-key" });
        Assert.Contains("taxRegimeCode", errors.Keys);
    }

    [Fact]
    public void KeepsMoralCreationBlockedUntilCapitalRegimeCatalogIsConfirmed()
    {
        var errors = CustomerCreateValidator.Validate(Valid() with
        {
            LegalPersonality = CustomerCreatePersonality.Moral,
            LegalName = "Synthetic Company",
            CapitalRegime = "22"
        });
        Assert.Contains("capitalRegime", errors.Keys);
    }

    private static CustomerCreateCommand Valid() => new(
        CustomerCreatePersonality.Individual, "AAA010101AAA", "Synthetic", "Person", "Test", null, null,
        new DateOnly(1980, 1, 1), 1, 1, 1, 1, "605",
        1, "55", "5555555555", null, null, "Test Contact", "test@example.invalid", [1, 2]);
}
