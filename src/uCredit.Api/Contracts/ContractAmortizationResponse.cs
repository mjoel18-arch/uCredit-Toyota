using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Api.Contracts;

public sealed record ContractAmortizationResponse(
    string ContractNumber,
    int FinancingType,
    int Version,
    ContractAmortizationPaymentResponse? DownPayment,
    IReadOnlyList<ContractAmortizationPaymentResponse> Payments)
{
    public static ContractAmortizationResponse FromModel(ContractAmortizationSchedule schedule) => new(
        schedule.ContractNumber,
        schedule.FinancingType,
        schedule.Version,
        schedule.DownPayment is null ? null : ContractAmortizationPaymentResponse.FromModel(schedule.DownPayment),
        schedule.Payments.Select(ContractAmortizationPaymentResponse.FromModel).ToArray());
}

public sealed record ContractAmortizationPaymentResponse(
    int PaymentNumber,
    string Status,
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
    decimal TotalPayment)
{
    public static ContractAmortizationPaymentResponse FromModel(ContractAmortizationPayment payment) => new(
        payment.PaymentNumber,
        payment.Status.ToString(),
        payment.StartDate,
        payment.EndDate,
        payment.DueDate,
        payment.CalculationBase,
        payment.OutstandingBalance,
        payment.Amortization,
        payment.Interest,
        payment.Iva,
        payment.Payment,
        payment.PaymentWithIva,
        payment.TotalPayment);
}