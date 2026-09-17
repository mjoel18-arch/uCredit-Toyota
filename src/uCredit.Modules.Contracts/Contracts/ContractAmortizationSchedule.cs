namespace UCredit.Modules.Contracts.Contracts;

public sealed record ContractAmortizationSchedule(
    string ContractNumber,
    int FinancingType,
    int Version,
    ContractAmortizationPayment? DownPayment,
    IReadOnlyList<ContractAmortizationPayment> Payments);

public sealed record ContractAmortizationPayment(
    long PaymentId,
    int PaymentNumber,
    int Version,
    ContractAmortizationPaymentStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly DueDate,
    decimal CalculationBase,
    decimal OutstandingBalance,
    decimal Amortization,
    decimal Interest,
    decimal Iva,
    decimal Payment,
    decimal PaymentWithIva,
    decimal TotalPayment);

public enum ContractAmortizationPaymentStatus
{
    Pending = 0,
    Generated = 1,
}