using UCredit.Modules.Customers.Customers;

namespace UCredit.Modules.Customers.UnitTests;

public sealed class CustomerCreateValidatorTests
{
    [Fact]
    public void RequiresPersonFieldsByPersonality()
    {
        var errors = CustomerCreateValidator.Validate(Valid() with { FirstName = null, PaternalSurname = null, MaternalSurname = null });
        Assert.Contains("firstName", errors.Keys);
        Assert.Contains("paternalSurname", errors.Keys);
    }

    [Fact]
    public void AcceptsPhysicalPersonalitiesWithoutEmailData()
    {
        foreach (var personality in new[] { CustomerCreatePersonality.Individual, CustomerCreatePersonality.IndividualBusiness })
        {
            var command = Valid() with { LegalPersonality = personality };
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

    [Fact]
    public void RequiresAtLeastOneRole()
    {
        var errors = CustomerCreateValidator.Validate(Valid() with { RoleCodes = [] });
        Assert.Contains("roleCodes", errors.Keys);
    }

    [Fact]
    public void RejectsDuplicateRoles()
    {
        var errors = CustomerCreateValidator.Validate(Valid() with { RoleCodes = [3, 3] });
        Assert.Contains("roleCodes", errors.Keys);
    }

    [Fact]
    public void AcceptsMultipleRolesWithoutRequiringCustomerRole()
    {
        var errors = CustomerCreateValidator.Validate(Valid() with { RoleCodes = [3, 25] });
        Assert.DoesNotContain("roleCodes", errors.Keys);
    }

    private static CustomerCreateCommand Valid() => new(
        CustomerCreatePersonality.Individual, "AAA010101AAA", "Synthetic", "Person", "Test", null, null,
        new DateOnly(1980, 1, 1), 1, 1, 1, 1, "605", [1]);
}
