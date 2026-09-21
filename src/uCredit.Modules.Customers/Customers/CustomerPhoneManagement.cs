namespace UCredit.Modules.Customers.Customers;

public sealed record ManagedCustomerPhone(
    int PhoneId,
    int PersonId,
    int PhoneTypeCode,
    string? LongDistanceCode,
    string? AreaCode,
    string? PhoneNumber,
    string? Extension,
    int StatusCode,
    string? InactiveReason,
    bool IsDefault,
    DateTime ModifiedAt,
    string? ContactName)
{
    public string Status => StatusCode switch
    {
        1 => "Active",
        2 => "Inactive",
        0 => "InheritedInactive",
        _ => "Unknown",
    };
}

public sealed record CustomerPhoneCreateCommand(
    int PersonId,
    int PhoneTypeCode,
    string? LongDistanceCode,
    string? AreaCode,
    string PhoneNumber,
    string? Extension,
    string? ContactName,
    bool IsDefault);

public sealed record CustomerPhoneUpdateCommand(
    int PersonId,
    int PhoneId,
    int PhoneTypeCode,
    string? LongDistanceCode,
    string? AreaCode,
    string PhoneNumber,
    string? Extension,
    string? ContactName,
    bool IsDefault,
    DateTime ExpectedModifiedAt);

public sealed record CustomerPhoneStateChangeCommand(
    int PersonId,
    int PhoneId,
    DateTime ExpectedModifiedAt,
    int? ReplacementPhoneId = null);

public sealed class CustomerPhoneValidationException(string message) : Exception(message);

public sealed class CustomerPhoneConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class CustomerPhoneNotFoundException(string message) : Exception(message);

public interface ICustomerPhoneReadRepository
{
    Task<IReadOnlyList<ManagedCustomerPhone>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default);
}

public interface ICustomerPhoneWriteRepository
{
    Task<ManagedCustomerPhone> CreateAsync(CustomerPhoneCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerPhone> UpdateAsync(CustomerPhoneUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerPhone> ActivateAsync(CustomerPhoneStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerPhone> DeactivateAsync(CustomerPhoneStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
}

public static class CustomerPhoneRules
{
    public static string? NormalizeOptional(string? value, int maximum, string name)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximum)
            throw new CustomerPhoneValidationException($"{name} must not exceed {maximum} characters.");
        return normalized;
    }

    public static string NormalizeRequired(string value, int maximum, string name)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximum)
            throw new CustomerPhoneValidationException($"{name} is required and must not exceed {maximum} characters.");
        return normalized;
    }

    public static void ValidateType(int phoneTypeCode)
    {
        if (phoneTypeCode <= 0)
            throw new CustomerPhoneValidationException("A phone type is required.");
    }
}
