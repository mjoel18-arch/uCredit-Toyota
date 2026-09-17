namespace UCredit.Modules.Contracts.Contracts;

public sealed record ContractSummary(
    string ContractNumber,
    int PersonId,
    string? PersonName,
    string? OperationTypeCode,
    string? OperationTypeName,
    int? AddressId,
    decimal? FinancedAmount,
    decimal? OutstandingBalance,
    string? CurrencyCode,
    string? CurrencyName,
    int CurrentTerm,
    int? OriginalTerm,
    DateOnly StartDate,
    DateOnly ActivationDate,
    DateOnly? DisbursementDate,
    DateOnly? FirstPaymentDate,
    DateOnly? LastPaymentDate,
    int? StatusCode,
    string? StatusName,
    string? ModifiedBy,
    DateTimeOffset? ModifiedAt);
