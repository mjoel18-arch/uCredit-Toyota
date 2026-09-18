namespace UCredit.Modules.Customers.Customers;

public static class CustomerSearchValidator
{
    public const int MaxRfcLength = 13;
    public const int MaxNameLength = 200;
    public const int MaxPageSize = 100;

    public static Dictionary<string, string[]> Validate(CustomerSearchCriteria criteria)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (criteria.PersonId is null && string.IsNullOrWhiteSpace(criteria.Rfc) && string.IsNullOrWhiteSpace(criteria.Name))
        {
            Add(errors, "criteria", "At least personId, rfc or name is required.");
        }

        if (criteria.PersonId is <= 0)
        {
            Add(errors, "personId", "personId must be greater than zero.");
        }

        if (criteria.Rfc is { Length: > MaxRfcLength })
        {
            Add(errors, "rfc", $"rfc must not exceed {MaxRfcLength} characters.");
        }

        if (criteria.Name is { Length: > MaxNameLength })
        {
            Add(errors, "name", $"name must not exceed {MaxNameLength} characters.");
        }

        if (criteria.LegalPersonalityCode is not null and not (1 or 2 or 20))
        {
            Add(errors, "legalPersonality", "legalPersonality must be one of 1, 2 or 20.");
        }

        if (criteria.Page < 1)
        {
            Add(errors, "page", "page must be at least 1.");
        }

        if (criteria.PageSize is < 1 or > MaxPageSize)
        {
            Add(errors, "pageSize", $"pageSize must be between 1 and {MaxPageSize}.");
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }
}
