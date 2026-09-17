namespace UCredit.Modules.Contracts.Contracts;

public interface IContractAmortizationReadRepository
{
    Task<ContractAmortizationSchedule?> GetScheduleByNumberAsync(
        string contractNumber,
        CancellationToken cancellationToken = default);
}