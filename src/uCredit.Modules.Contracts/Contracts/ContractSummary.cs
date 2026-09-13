namespace UCredit.Modules.Contracts.Contracts;

public sealed record ContractSummary(
    string ContractNumber,
    int PersonId,
    string PersonName,
    string OperationTypeCode,
    string OperationTypeName,
    int? AddressId,
    decimal FinancedAmount,
    decimal OutstandingBalance,
    DateOnly? DisbursementDate,
    DateOnly? FirstPaymentDate,
    DateOnly? LastPaymentDate,
    int StatusCode,
    string StatusName,
    string? ModifiedBy,
    DateTimeOffset? ModifiedAt);

