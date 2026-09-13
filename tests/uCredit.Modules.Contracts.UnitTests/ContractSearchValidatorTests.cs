using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Modules.Contracts.UnitTests;

public sealed class ContractSearchValidatorTests
{
    [Fact]
    public void ValidateWithoutPrimaryCriterionReturnsError()
    {
        var criteria = new ContractSearchCriteria(null, null, null, null, null, null, null, null);

        var errors = ContractSearchValidator.Validate(criteria);

        Assert.Contains("criteria", errors.Keys);
    }

    [Fact]
    public void ValidateWithContractNumberIsValid()
    {
        var criteria = new ContractSearchCriteria("CR2300054", null, null, null, null, null, null, null);

        var errors = ContractSearchValidator.Validate(criteria);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateWithSupportedExactFiltersIsValid()
    {
        var criteria = new ContractSearchCriteria(null, 42, "RFC123", null, null, null, null, 7, PageSize: 10);

        var errors = ContractSearchValidator.Validate(criteria);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateWithUnsupportedPartialFiltersReturnsErrors()
    {
        var criteria = new ContractSearchCriteria(null, null, null, "ACME", "VIN", "APP", null, null);

        var errors = ContractSearchValidator.Validate(criteria);

        Assert.Contains("personName", errors.Keys);
        Assert.Contains("vin", errors.Keys);
        Assert.Contains("applicationNumber", errors.Keys);
    }

    [Fact]
    public void ValidateWithPageSizeOutsideRangeReturnsError()
    {
        var criteria = new ContractSearchCriteria("CR2300054", null, null, null, null, null, null, null, PageSize: 101);

        var errors = ContractSearchValidator.Validate(criteria);

        Assert.Contains("pageSize", errors.Keys);
    }
}
