namespace UCredit.Modules.Contracts.Contracts;

public static class ContractSearchValidator
{
    public static IReadOnlyDictionary<string, string[]> Validate(ContractSearchCriteria criteria)
    {
        var errors = new Dictionary<string, string[]>();

        if (!criteria.HasPrimaryCriterion)
        {
            errors["criteria"] = ["At least one primary search criterion is required."];
        }

        if (criteria.Page < 1)
        {
            errors["page"] = ["Page must be greater than zero."];
        }

        if (criteria.PageSize is < 10 or > 100)
        {
            errors["pageSize"] = ["Page size must be between 10 and 100."];
        }

        AddMaxLength(errors, "contractNumber", criteria.ContractNumber, 15);
        AddMaxLength(errors, "rfc", criteria.Rfc, 13);
        AddMaxLength(errors, "vin", criteria.Vin, 20);
        AddMaxLength(errors, "applicationNumber", criteria.ApplicationNumber, 15);
        AddMaxLength(errors, "operationType", criteria.OperationType, 4);

        return errors;
    }

    private static void AddMaxLength(
		Dictionary<string, string[]> errors,
        string field,
        string? value,
        int maximum)
    {
        if (value?.Length > maximum)
        {
            errors[field] = [$"{field} must not exceed {maximum} characters."];
        }
    }
}

