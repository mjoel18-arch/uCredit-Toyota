using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Contracts;

public sealed record ManagedCustomerPhoneResponse(
    int PhoneId,
    int PersonId,
    int PhoneTypeCode,
    int AddressId,
    string? LongDistanceCode,
    string? AreaCode,
    string? PhoneNumber,
    string? Extension,
    string Status,
    string? InactiveReason,
    bool IsDefault,
    DateTime ModifiedAt,
    string? ContactName)
{
    public static ManagedCustomerPhoneResponse FromModel(ManagedCustomerPhone phone) => new(
        phone.PhoneId, phone.PersonId, phone.PhoneTypeCode, phone.AddressId,
        phone.LongDistanceCode, phone.AreaCode, phone.PhoneNumber,
        phone.Extension, phone.Status, phone.InactiveReason, phone.IsDefault,
        phone.ModifiedAt, phone.ContactName);
}
