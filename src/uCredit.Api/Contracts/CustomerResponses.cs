using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Contracts;

public sealed record CustomerListResponse(
    int PersonId,
    string? RfcMasked,
    string Name,
    CustomerLegalPersonalityResponse LegalPersonality,
    CustomerStatusResponse Status,
    CustomerAddressResponse? PrimaryAddress,
    CustomerPhoneResponse? PrimaryPhone,
    IReadOnlyList<CustomerRoleResponse> Roles)
{
    public static CustomerListResponse FromModel(Customer customer) => new(
        customer.PersonId,
        MaskRfc(customer.Rfc),
        customer.Name,
        new CustomerLegalPersonalityResponse(customer.LegalPersonalityCode, customer.LegalPersonalityDescription),
        new CustomerStatusResponse(customer.StatusCode, customer.StatusDescription),
        CustomerAddressResponse.FromModel(customer.PrimaryAddress),
        CustomerPhoneResponse.FromNullable(customer.PrimaryPhone),
        customer.Roles.Select(CustomerRoleResponse.FromModel).ToArray());

    internal static string? MaskRfc(string? rfc)
    {
        if (string.IsNullOrEmpty(rfc)) return null;
        if (rfc.Length <= 4) return new string('*', rfc.Length);
        var visiblePrefix = Math.Min(4, rfc.Length - 2);
        return rfc[..visiblePrefix] + new string('*', rfc.Length - visiblePrefix - 2) + rfc[^2..];
    }
}

public sealed record CustomerDetailResponse(
    int PersonId,
    string? Rfc,
    string Name,
    CustomerLegalPersonalityResponse LegalPersonality,
    CustomerStatusResponse Status,
    CustomerAddressResponse? PrimaryAddress,
    CustomerPhoneResponse? PrimaryPhone,
    IReadOnlyList<CustomerPhoneResponse> ActivePhones,
    IReadOnlyList<CustomerEmailResponse> ActiveEmails,
    IReadOnlyList<CustomerRoleResponse> Roles)
{
    public static CustomerDetailResponse FromModel(Customer customer) => new(
        customer.PersonId,
        customer.Rfc,
        customer.Name,
        new CustomerLegalPersonalityResponse(customer.LegalPersonalityCode, customer.LegalPersonalityDescription),
        new CustomerStatusResponse(customer.StatusCode, customer.StatusDescription),
        CustomerAddressResponse.FromModel(customer.PrimaryAddress),
        CustomerPhoneResponse.FromNullable(customer.PrimaryPhone),
        customer.ActivePhones.Select(CustomerPhoneResponse.FromModel).ToArray(),
        customer.ActiveEmails.Select(CustomerEmailResponse.FromModel).ToArray(),
        customer.Roles.Select(CustomerRoleResponse.FromModel).ToArray());
}

public sealed record CustomerProfileReadinessResponse(
    int PersonId,
    bool HasGeneralData,
    bool HasAddress,
    bool HasPhone,
    bool HasAccount,
    bool CanCreateContract,
    IReadOnlyList<string> MissingRequirements)
{
    public static CustomerProfileReadinessResponse FromModel(CustomerProfileReadiness readiness) => new(
        readiness.PersonId,
        readiness.HasGeneralData,
        readiness.HasAddress,
        readiness.HasPhone,
        readiness.HasAccount,
        readiness.CanCreateContract,
        readiness.MissingRequirements.Select(ToWireValue).ToArray());

    private static string ToWireValue(CustomerProfileRequirement requirement) => requirement switch
    {
        CustomerProfileRequirement.GeneralData => "generalData",
        CustomerProfileRequirement.Address => "address",
        CustomerProfileRequirement.Phone => "phone",
        CustomerProfileRequirement.Account => "account",
        _ => throw new ArgumentOutOfRangeException(nameof(requirement)),
    };
}

public sealed record CustomerLegalPersonalityResponse(int Code, string? Description);
public sealed record CustomerStatusResponse(int Code, string? Description);
public sealed record CustomerRoleResponse(int Code, string? Description)
{
    public static CustomerRoleResponse FromModel(CustomerRole role) => new(role.Code, role.Description);
}

public sealed record CustomerAddressResponse(
    int AddressId,
    string? PostalCode,
    string? State,
    string? Municipality,
    string? City,
    string? Neighborhood,
    string? StreetAndNumber,
    string? ExteriorNumber,
    string? InteriorNumber,
    int TypeCode,
    string? TypeDescription)
{
    public static CustomerAddressResponse? FromModel(CustomerAddress? address) => address is null
        ? null
        : new(address.AddressId, address.PostalCode, address.State, address.Municipality, address.City, address.Neighborhood, address.StreetAndNumber, address.ExteriorNumber, address.InteriorNumber, address.AddressTypeCode, address.AddressTypeDescription);
}

public sealed record CustomerPhoneResponse(
    int PhoneId,
    string? AreaCode,
    string? PhoneNumber,
    string? Extension,
    bool IsDefault)
{
    public static CustomerPhoneResponse? FromNullable(CustomerPhone? phone) => phone is null
        ? null
        : new(phone.PhoneId, phone.AreaCode, phone.PhoneNumber, phone.Extension, phone.IsDefault);

    public static CustomerPhoneResponse FromModel(CustomerPhone phone) => new(phone.PhoneId, phone.AreaCode, phone.PhoneNumber, phone.Extension, phone.IsDefault);
}

public sealed record CustomerEmailResponse(int EmailId, string? Contact, string? Email)
{
    public static CustomerEmailResponse FromModel(CustomerEmail email) => new(email.EmailId, email.Contact, email.Email);
}
