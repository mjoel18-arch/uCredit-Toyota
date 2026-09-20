using UCredit.Api.Contracts;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/customers")
            .WithTags("Customers")
            .RequireAuthorization("customers.read");

        group.MapGet("/", SearchCustomersAsync).WithName("SearchCustomers");
        group.MapGet("/{personId:int}/readiness", GetReadinessAsync).WithName("GetCustomerProfileReadiness");
        group.MapGet("/{personId:int}", GetCustomerAsync).WithName("GetCustomer");
        return endpoints;
    }

    private static async Task<IResult> SearchCustomersAsync(
        [AsParameters] CustomerSearchRequest request,
        ICustomerReadRepository repository,
        CancellationToken cancellationToken)
    {
        var criteria = request.ToCriteria();
        var errors = CustomerSearchValidator.Validate(criteria);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await repository.SearchAsync(criteria, cancellationToken);
        return Results.Ok(new CustomerPagedResponse(
            result.Items.Select(CustomerListResponse.FromModel).ToArray(), result.Page, result.PageSize, result.Total));
    }

    private static async Task<IResult> GetCustomerAsync(
        int personId,
        ICustomerReadRepository repository,
        CancellationToken cancellationToken)
    {
        if (personId <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["personId"] = ["personId must be greater than zero."]
            });
        }

        var customer = await repository.GetByPersonIdAsync(personId, cancellationToken);
        return customer is null ? Results.NotFound() : Results.Ok(CustomerDetailResponse.FromModel(customer));
    }

    private static async Task<IResult> GetReadinessAsync(
        int personId,
        IExecutionTenantContext executionTenantContext,
        ICustomerProfileReadinessService service,
        CancellationToken cancellationToken)
    {
        if (personId <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["personId"] = ["personId must be greater than zero."]
            });
        }

        if (await executionTenantContext.GetAsync(cancellationToken) is null)
            return Results.NotFound();

        var readiness = await service.GetAsync(personId, cancellationToken);
        return readiness is null ? Results.NotFound() : Results.Ok(CustomerProfileReadinessResponse.FromModel(readiness));
    }
}

public sealed record CustomerSearchRequest(
    int? PersonId,
    string? Rfc,
    string? Name,
    int? LegalPersonality,
    int Page = 1,
    int PageSize = 20,
    CustomerSort Sort = CustomerSort.PersonIdAsc)
{
    public CustomerSearchCriteria ToCriteria() => new(PersonId, Rfc, Name, LegalPersonality, Page, PageSize, Sort);
}

public sealed record CustomerPagedResponse(
    IReadOnlyList<CustomerListResponse> Items,
    int Page,
    int PageSize,
    int Total);
