namespace UCredit.Modules.Contracts.Contracts;

public interface IContractReadRepository
{
    Task<PagedResult<ContractSummary>> SearchAsync(
        ContractSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<ContractSummary?> GetByNumberAsync(
        string contractNumber,
        CancellationToken cancellationToken = default);
}

