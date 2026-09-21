namespace UCredit.Modules.Customers.Customers;

public static class CustomerAddressUse
{
    public const string Billing = "billing";
    public const string Statements = "statements";
    public const string Other = "other";

    public static readonly IReadOnlySet<string> Allowed =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Billing,
            Statements,
            Other,
        };
}

public sealed record ManagedCustomerAddress(
    int AddressId,
    int PersonId,
    string? PostalCode,
    string? State,
    string? Municipality,
    string? City,
    string? Neighborhood,
    string? StreetAndNumber,
    string? ExteriorNumber,
    string? InteriorNumber,
    int AddressTypeCode,
    string? AddressTypeDescription,
    IReadOnlyList<string> Uses,
    bool IsActive,
    bool IsDefault,
    DateTime ModifiedAt,
    int CountryCode);

public sealed record CustomerAddressCreateCommand(
    int PersonId,
    string PostalCode,
    string State,
    string Municipality,
    string City,
    string Neighborhood,
    string StreetAndNumber,
    string ExteriorNumber,
    string? InteriorNumber,
    string? Reference,
    string? Schedule,
    int AddressTypeCode,
    IReadOnlyList<string>? Uses,
    bool IsDefault,
    int CountryCode);

public sealed record CustomerAddressUpdateCommand(
    int PersonId,
    int AddressId,
    string PostalCode,
    string State,
    string Municipality,
    string City,
    string Neighborhood,
    string StreetAndNumber,
    string ExteriorNumber,
    string? InteriorNumber,
    string? Reference,
    string? Schedule,
    int AddressTypeCode,
    IReadOnlyList<string>? Uses,
    bool IsDefault,
    DateTime ExpectedModifiedAt,
    int CountryCode);

public sealed record CustomerAddressStateChangeCommand(
    int PersonId,
    int AddressId,
    DateTime ExpectedModifiedAt,
    int? ReplacementAddressId = null);

public sealed class CustomerAddressValidationException(string message) : Exception(message);

public sealed class CustomerAddressConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public interface ICustomerAddressReadRepository
{
    Task<IReadOnlyList<ManagedCustomerAddress>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default);
}

public interface ICustomerAddressWriteRepository
{
    Task<ManagedCustomerAddress> CreateAsync(CustomerAddressCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerAddress> UpdateAsync(CustomerAddressUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerAddress> ActivateAsync(CustomerAddressStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerAddress> DeactivateAsync(CustomerAddressStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
}

public static class CustomerAddressRules
{
    public static IReadOnlyList<string> NormalizeUses(IReadOnlyList<string>? uses, int addressTypeCode)
    {
        var normalized = (uses ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Any(value => !CustomerAddressUse.Allowed.Contains(value)))
            throw new CustomerAddressValidationException("Address uses contain an unsupported value.");

        if (addressTypeCode is < 1 or > 4)
            throw new CustomerAddressValidationException("Only address types 1 through 4 are supported.");

        if (addressTypeCode == 1 && !CustomerAddressUse.Allowed.SetEquals(normalized))
            throw new CustomerAddressValidationException("A unique address requires all supported uses.");

        if (addressTypeCode == 2 && !normalized.Contains(CustomerAddressUse.Billing, StringComparer.Ordinal))
            throw new CustomerAddressValidationException("A fiscal address requires billing.");

        if (addressTypeCode is 3 or 4 && normalized.Contains(CustomerAddressUse.Billing, StringComparer.Ordinal))
            throw new CustomerAddressValidationException("Administrative and social addresses cannot use billing.");

        return normalized;
    }

    public static (int Billing, int Statements, int Other) ToLegacyFlags(int addressTypeCode, IReadOnlyList<string>? uses)
    {
        var normalized = NormalizeUses(uses, addressTypeCode);
        return addressTypeCode switch
        {
            1 => (1, 1, 1),
            2 => (1, normalized.Contains(CustomerAddressUse.Statements, StringComparer.Ordinal) ? 1 : 0, normalized.Contains(CustomerAddressUse.Other, StringComparer.Ordinal) ? 1 : 0),
            3 or 4 => (0, normalized.Contains(CustomerAddressUse.Statements, StringComparer.Ordinal) ? 1 : 0, normalized.Contains(CustomerAddressUse.Other, StringComparer.Ordinal) ? 1 : 0),
            _ => throw new CustomerAddressValidationException("Only address types 1 through 4 are supported."),
        };
    }
}
