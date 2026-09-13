namespace UCredit.Modules.Contracts.Contracts;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);

