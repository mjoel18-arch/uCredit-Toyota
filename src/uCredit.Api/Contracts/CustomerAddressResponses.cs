using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Contracts;

public sealed record ManagedCustomerAddressResponse(
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
    int CountryCode)
{
    public static ManagedCustomerAddressResponse FromModel(ManagedCustomerAddress address) => new(
        address.AddressId,
        address.PersonId,
        address.PostalCode,
        address.State,
        address.Municipality,
        address.City,
        address.Neighborhood,
        address.StreetAndNumber,
        address.ExteriorNumber,
        address.InteriorNumber,
        address.AddressTypeCode,
        address.AddressTypeDescription,
        address.Uses,
        address.IsActive,
        address.IsDefault,
        address.ModifiedAt,
        address.CountryCode);
}
