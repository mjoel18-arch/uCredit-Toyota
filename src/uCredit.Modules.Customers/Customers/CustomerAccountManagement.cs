namespace UCredit.Modules.Customers.Customers;

public sealed record ManagedCustomerAccount(
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
    DateTime ModifiedAt);

public sealed record CustomerBank(short BankId, string BankName);

public interface ICustomerBankReadRepository
{
    Task<IReadOnlyList<CustomerBank>> GetActiveRealAsync(CancellationToken cancellationToken = default);
}

public sealed record CustomerAccountCreateCommand(
    int PersonId,
    short BankId,
    short BranchNumber,
    byte CurrencyCode,
    byte AccountTypeCode,
    string AccountNumber,
    string Clabe);

public sealed record CustomerAccountUpdateCommand(
    int PersonId,
    int AccountId,
    short BankId,
    short BranchNumber,
    byte CurrencyCode,
    byte AccountTypeCode,
    string? AccountNumber,
    string? Clabe,
    DateTime ExpectedModifiedAt);

public sealed record CustomerAccountStateChangeCommand(int PersonId, int AccountId, DateTime ExpectedModifiedAt);

public sealed class CustomerAccountValidationException(string message) : Exception(message);
public sealed class CustomerAccountNotFoundException(string message) : Exception(message);
public sealed class CustomerAccountConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public interface ICustomerAccountReadRepository
{
    Task<IReadOnlyList<ManagedCustomerAccount>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default);
}

public interface ICustomerAccountWriteRepository
{
    Task<ManagedCustomerAccount> CreateAsync(CustomerAccountCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerAccount> UpdateAsync(CustomerAccountUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerAccount> ActivateAsync(CustomerAccountStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
    Task<ManagedCustomerAccount> DeactivateAsync(CustomerAccountStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
}

public static class CustomerAccountRules
{
    public static void ValidateBranch(short branchNumber)
    {
        if (branchNumber < 0) throw new CustomerAccountValidationException("Branch number must be between 0 and 32767.");
    }

    public static string RequiredSensitive(string value, int maximum, string name)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximum || normalized.Any(char.IsWhiteSpace) || !normalized.All(char.IsDigit))
            throw new CustomerAccountValidationException($"{name} must be numeric and have a valid length.");
        return normalized;
    }

    public static string RequiredClabe(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length != 18 || !normalized.All(char.IsDigit))
            throw new CustomerAccountValidationException("CLABE must contain exactly 18 digits.");
        return normalized;
    }
}
