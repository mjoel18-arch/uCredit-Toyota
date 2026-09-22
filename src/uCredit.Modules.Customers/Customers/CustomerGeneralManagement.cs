namespace UCredit.Modules.Customers.Customers;

public sealed record CustomerGeneralRole(
    int Code,
    string? Description,
    bool IsActive,
    bool IsEditable);

public sealed record CustomerGeneralProfile(
    int PersonId,
    int LegalPersonalityCode,
    string? Rfc,
    string? FirstName,
    string? PaternalSurname,
    string? MaternalSurname,
    DateTime? BirthDate,
    string? LegalName,
    string? ContactName,
    string? ContactPosition,
    int StatusCode,
    DateTime PersonModifiedAt,
    DateTime SubtypeModifiedAt,
    IReadOnlyList<CustomerGeneralRole> Roles);

public sealed record CustomerGeneralUpdateCommand(
    int PersonId,
    string? FirstName,
    string? PaternalSurname,
    string? MaternalSurname,
    DateTime? BirthDate,
    string? LegalName,
    string? ContactName,
    string? ContactPosition,
    IReadOnlyList<int>? RoleCodes,
    DateTime ExpectedPersonModifiedAt,
    DateTime ExpectedSubtypeModifiedAt);

public sealed class CustomerGeneralValidationException(string message) : Exception(message);

public sealed class CustomerGeneralConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class CustomerGeneralNotFoundException(string message) : Exception(message);

public interface ICustomerGeneralRepository
{
    Task<CustomerGeneralProfile?> GetAsync(int personId, CancellationToken cancellationToken = default);

    Task<CustomerGeneralProfile> UpdateAsync(
        CustomerGeneralUpdateCommand command,
        string legacyUserCode,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public static class CustomerGeneralRules
{
    public static string? Optional(string? value, int maximum, string field)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (normalized.Length > maximum)
            throw new CustomerGeneralValidationException($"{field} must not exceed {maximum} characters.");
        return normalized;
    }

    public static void ValidateDate(DateTime? value, string field)
    {
        if (value is not null && value.Value.Date > DateTime.UtcNow.Date)
            throw new CustomerGeneralValidationException($"{field} cannot be in the future.");
    }
}
