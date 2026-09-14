using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Modules.Contracts.UnitTests;

public sealed class ContractSearchValidatorTests
{
    [Fact]
    public void ValidateWithoutPrimaryCriterionReturnsError()
    {
        var errors = ContractSearchValidator.Validate(new ContractSearchCriteria(null, null, null, null, null, null, null));
        Assert.Contains("criteria", errors.Keys);
    }

    [Fact]
    public void ValidateWithContractNumberIsValid()
    {
        var errors = ContractSearchValidator.Validate(new ContractSearchCriteria("CR2300054", null, null, null, null, null, null));
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateWithPrefixAndVinFiltersIsValid()
    {
        var errors = ContractSearchValidator.Validate(new ContractSearchCriteria(null, null, null, "ACME", "VIN", null, null));
        Assert.Empty(errors);
    }


    [Fact]
    public void ValidateVinWithTwentyCharactersIsValid()
    {
        var errors = ContractSearchValidator.Validate(new ContractSearchCriteria(null, null, null, null, new string('V', 20), null, null));
        Assert.DoesNotContain("vin", errors.Keys);
    }

    [Fact]
    public void ValidateVinWithMoreThanTwentyCharactersReturnsError()
    {
        var errors = ContractSearchValidator.Validate(new ContractSearchCriteria(null, null, null, null, new string('V', 21), null, null));
        Assert.Contains("vin", errors.Keys);
    }

    [Fact]
    public void ValidateWithPageSizeOutsideRangeReturnsError()
    {
        var errors = ContractSearchValidator.Validate(new ContractSearchCriteria("CR2300054", null, null, null, null, null, null, PageSize: 101));
        Assert.Contains("pageSize", errors.Keys);
    }
}
