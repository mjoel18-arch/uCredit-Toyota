namespace UCredit.Modules.Customers.Customers;

public sealed record CustomerEmailUsage(int Code, string Description);

public sealed record ManagedCustomerEmail(
    int EmailId,
    int PersonId,
    string? Contact,
    string Email,
    int StatusCode,
    IReadOnlyList<int> UsageCodes,
    DateTime ModifiedAt)
{
    public string Status => StatusCode switch
    {
        1 => "Active",
        2 => "Inactive",
        _ => "Unknown",
    };
}

public sealed record CustomerEmailCreateCommand(
    int PersonId,
    string? Contact,
    string Email,
    IReadOnlyList<int> UsageCodes);

public sealed record CustomerEmailUpdateCommand(
    int PersonId,
    int EmailId,
    string? Contact,
    string? Email,
    IReadOnlyList<int> UsageCodes,
    DateTime ExpectedModifiedAt);

public sealed record CustomerEmailStateChangeCommand(
    int PersonId,
    int EmailId,
    DateTime ExpectedModifiedAt);

public sealed class CustomerEmailValidationException(string message) : Exception(message);

public sealed class CustomerEmailConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class CustomerEmailNotFoundException(string message) : Exception(message);

public interface ICustomerEmailReadRepository
{
    Task<IReadOnlyList<ManagedCustomerEmail>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default);
}

public interface ICustomerEmailUsageRepository
{
    Task<IReadOnlyList<CustomerEmailUsage>> GetActiveUsagesAsync(CancellationToken cancellationToken = default);
}

public interface ICustomerEmailWriteRepository
{
    Task<ManagedCustomerEmail> CreateAsync(CustomerEmailCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerEmail> UpdateAsync(CustomerEmailUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerEmail> ActivateAsync(CustomerEmailStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerEmail> DeactivateAsync(CustomerEmailStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
}

public static class CustomerEmailRules
{
    public static string NormalizeEmail(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > 250 || !System.Net.Mail.MailAddress.TryCreate(normalized, out _))
            throw new CustomerEmailValidationException("Email is required, valid and must not exceed 250 characters.");
        return normalized;
    }

    public static string? NormalizeContact(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > 250)
            throw new CustomerEmailValidationException("Contact must not exceed 250 characters.");
        return normalized;
    }

    public static int[] NormalizeUsages(IReadOnlyList<int> usageCodes)
    {
        if (usageCodes.Count == 0 || usageCodes.Any(code => code <= 0) || usageCodes.Count != usageCodes.Distinct().Count())
            throw new CustomerEmailValidationException("At least one active email use is required.");
        return usageCodes.ToArray();
    }
}
