namespace UCredit.Modules.Customers.Customers;

public sealed record Customer(
    int PersonId,
    string? Rfc,
    string Name,
    int LegalPersonalityCode,
    string? LegalPersonalityDescription,
    int StatusCode,
    string? StatusDescription,
    CustomerAddress? PrimaryAddress,
    CustomerPhone? PrimaryPhone,
    IReadOnlyList<CustomerRole> Roles,
    IReadOnlyList<CustomerPhone> ActivePhones,
    IReadOnlyList<CustomerEmail> ActiveEmails);

public sealed record CustomerAddress(
    int AddressId,
    string? PostalCode,
    string? State,
    string? Municipality,
    string? City,
    string? Neighborhood,
    string? StreetAndNumber,
    string? ExteriorNumber,
    string? InteriorNumber,
    int AddressTypeCode,
    string? AddressTypeDescription);

public sealed record CustomerPhone(
    int PhoneId,
    string? AreaCode,
    string? PhoneNumber,
    string? Extension,
    bool IsDefault);

public sealed record CustomerEmail(
    int EmailId,
    string? Contact,
    string? Email);

public sealed record CustomerRole(int Code, string? Description);

public static class CustomerPhoneSelector
{
    public static CustomerPhone? SelectPrimary(IEnumerable<CustomerPhone> activePhones) =>
        activePhones.Where(phone => phone.IsDefault).OrderBy(phone => phone.PhoneId).FirstOrDefault();
}
