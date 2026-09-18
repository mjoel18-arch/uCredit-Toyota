namespace UCredit.Modules.Customers.Customers;

public enum CustomerSort
{
    PersonIdAsc,
    NameAsc,
    NameDesc
}

public sealed record CustomerSearchCriteria(
    int? PersonId = null,
    string? Rfc = null,
    string? Name = null,
    int? LegalPersonalityCode = null,
    int Page = 1,
    int PageSize = 20,
    CustomerSort Sort = CustomerSort.PersonIdAsc);
