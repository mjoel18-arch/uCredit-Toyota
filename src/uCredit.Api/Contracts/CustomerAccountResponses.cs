using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Contracts;

public sealed record ManagedCustomerAccountResponse(
    int AccountId,
    int PersonId,
    short BankId,
    string? BankName,
    short BranchNumber,
    byte CurrencyCode,
    string? CurrencyName,
    byte AccountTypeCode,
    string? AccountTypeName,
    int? PaymentMethodCode,
    byte Status,
    string? MaskedAccountNumber,
    string? MaskedClabe,
    DateTime ModifiedAt)
{
    public static ManagedCustomerAccountResponse FromModel(ManagedCustomerAccount account) => new(
        account.AccountId, account.PersonId, account.BankId, account.BankName,
        account.BranchNumber, account.CurrencyCode,
        account.CurrencyName, account.AccountTypeCode, account.AccountTypeName,
        account.PaymentMethodCode, account.Status, account.MaskedAccountNumber,
        account.MaskedClabe, account.ModifiedAt);
}
