namespace UCredit.Modules.Contracts.Contracts;

public sealed record ContractSearchCriteria(
    string? ContractNumber,
    int? PersonId,
    string? Rfc,
    string? PersonName,
    string? Vin,
    string? ApplicationNumber,
    string? OperationType,
    int? Status,
    int Page = 1,
    int PageSize = 20,
    ContractSort Sort = ContractSort.ContractNumberAsc)
{
    public bool HasPrimaryCriterion =>
        !string.IsNullOrWhiteSpace(ContractNumber) ||
        PersonId is > 0 ||
        !string.IsNullOrWhiteSpace(Rfc) ||
        !string.IsNullOrWhiteSpace(PersonName) ||
        !string.IsNullOrWhiteSpace(Vin) ||
        !string.IsNullOrWhiteSpace(ApplicationNumber);
}

public enum ContractSort
{
    ContractNumberAsc,
    ContractNumberDesc,
    OutstandingBalanceAsc,
    OutstandingBalanceDesc
}

