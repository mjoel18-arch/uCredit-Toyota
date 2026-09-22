using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Contracts;

public sealed record ManagedCustomerEmailResponse(
    int EmailId,
    int PersonId,
    string? Contact,
    string Email,
    string Status,
    IReadOnlyList<int> UsageCodes,
    DateTime ModifiedAt)
{
    public static ManagedCustomerEmailResponse FromModel(ManagedCustomerEmail email) => new(
        email.EmailId, email.PersonId, email.Contact, email.Email, email.Status,
        email.UsageCodes, email.ModifiedAt);
}

public sealed record CustomerEmailUsageResponse(int Code, string Description);
