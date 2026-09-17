using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Api.Contracts;

public sealed record ContractDetailResponse(
    string ContractNumber,
    ContractStatusResponse? Status,
    ContractOperationResponse? OperationType,
    string? CustomerName,
    string? CurrencyCode,
    string? CurrencyName,
    decimal? FinancedAmount,
    decimal? OutstandingBalance,
    int CurrentTerm,
    int? OriginalTerm,
    DateOnly StartDate,
    DateOnly ActivationDate,
    DateOnly? DisbursementDate,
    DateOnly? FirstPaymentDate,
    DateOnly? LastPaymentDate)
{
    public static ContractDetailResponse FromSummary(ContractSummary summary) => new(
        summary.ContractNumber,
        summary.StatusCode is null && summary.StatusName is null
            ? null
            : new ContractStatusResponse(summary.StatusCode, summary.StatusName),
        summary.OperationTypeCode is null && summary.OperationTypeName is null
            ? null
            : new ContractOperationResponse(summary.OperationTypeCode, summary.OperationTypeName),
        summary.PersonName,
        summary.CurrencyCode,
        summary.CurrencyName,
        summary.FinancedAmount,
        summary.OutstandingBalance,
        summary.CurrentTerm,
        summary.OriginalTerm,
        summary.StartDate,
        summary.ActivationDate,
        summary.DisbursementDate,
        summary.FirstPaymentDate,
        summary.LastPaymentDate);
}

public sealed record ContractStatusResponse(int? Code, string? Description);

public sealed record ContractOperationResponse(string? Code, string? Description);