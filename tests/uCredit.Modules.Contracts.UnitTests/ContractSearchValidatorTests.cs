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
}

