using Microsoft.AspNetCore.Mvc;
using UCredit.Api.Contracts;
using UCredit.Api.Security;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Endpoints;

public static class CustomerAccountEndpoints
{
    public static IEndpointRouteBuilder MapCustomerAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/customers/{personId:int}/accounts", GetAsync).WithTags("Customers").RequireAuthorization("customers.read");
        var group = endpoints.MapGroup("/api/v1/customers/{personId:int}/accounts").WithTags("Customers").RequireAuthorization("customers.write").AddEndpointFilter<AntiforgeryEndpointFilter>();
        group.MapPost("", CreateAsync); group.MapPut("{accountId:int}", UpdateAsync); group.MapPost("{accountId:int}/activate", ActivateAsync); group.MapPost("{accountId:int}/deactivate", DeactivateAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(int personId, [FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] ICustomerAccountReadRepository repository, CancellationToken cancellationToken)
    {
        if (personId <= 0) return InvalidPersonId();
        if (!await ValidTenant(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        var accounts = await repository.GetByPersonIdAsync(personId, cancellationToken);
        return accounts is null ? Results.NotFound() : Results.Ok(accounts.Select(ManagedCustomerAccountResponse.FromModel).ToArray());
    }

    private static Task<IResult> CreateAsync(int personId, [FromBody] CustomerAccountRequest request, [FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] IExecutionActorContext actorContext, [FromServices] ICustomerAccountWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken) => MutateAsync(personId, null, request, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);
    private static Task<IResult> UpdateAsync(int personId, int accountId, [FromBody] CustomerAccountRequest request, [FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] IExecutionActorContext actorContext, [FromServices] ICustomerAccountWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken) => MutateAsync(personId, accountId, request, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);

    private static async Task<IResult> MutateAsync(int personId, int? accountId, CustomerAccountRequest request, IExecutionTenantContext tenantContext, IConfiguration configuration, IExecutionActorContext actorContext, ICustomerAccountWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (personId <= 0 || accountId is <= 0) return InvalidPersonId();
        if (!await ValidTenant(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var actor = await actorContext.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(actor.LegacyUserCode)) return Results.UnprocessableEntity(new ProblemDetails { Title = "Legacy user code is required.", Status = 422 });
            ManagedCustomerAccount result = accountId is null
                ? await repository.CreateAsync(request.ToCreateCommand(personId), actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken)
                : await repository.UpdateAsync(request.ToUpdateCommand(personId, accountId.Value), actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return accountId is null ? Results.Created($"/api/v1/customers/{personId}/accounts/{result.AccountId}", ManagedCustomerAccountResponse.FromModel(result)) : Results.Ok(ManagedCustomerAccountResponse.FromModel(result));
        }
        catch (Exception exception) { return MapFailure(exception, httpContext.TraceIdentifier); }
    }

    private static Task<IResult> ActivateAsync(int personId, int accountId, [FromBody] AccountStateChangeRequest request, [FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] IExecutionActorContext actorContext, [FromServices] ICustomerAccountWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken) => ChangeStateAsync(personId, accountId, request, true, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);
    private static Task<IResult> DeactivateAsync(int personId, int accountId, [FromBody] AccountStateChangeRequest request, [FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] IExecutionActorContext actorContext, [FromServices] ICustomerAccountWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken) => ChangeStateAsync(personId, accountId, request, false, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);

    private static async Task<IResult> ChangeStateAsync(int personId, int accountId, AccountStateChangeRequest request, bool activate, IExecutionTenantContext tenantContext, IConfiguration configuration, IExecutionActorContext actorContext, ICustomerAccountWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (personId <= 0 || accountId <= 0) return InvalidPersonId();
        if (!await ValidTenant(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var actor = await actorContext.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(actor.LegacyUserCode)) return Results.UnprocessableEntity(new ProblemDetails { Title = "Legacy user code is required.", Status = 422 });
            var command = new CustomerAccountStateChangeCommand(personId, accountId, request.ExpectedModifiedAt ?? throw new CustomerAccountValidationException("expectedModifiedAt is required."));
            var result = activate ? await repository.ActivateAsync(command, actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken) : await repository.DeactivateAsync(command, actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok(ManagedCustomerAccountResponse.FromModel(result));
        }
        catch (Exception exception) { return MapFailure(exception, httpContext.TraceIdentifier); }
    }

    private static async Task<bool> ValidTenant(IExecutionTenantContext context, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var tenant = await context.GetAsync(cancellationToken); var deployment = configuration["Deployment:TenantCode"];
        return tenant is not null && !string.IsNullOrWhiteSpace(deployment) && string.Equals(tenant.TenantCode, deployment, StringComparison.Ordinal);
    }

    private static IResult MapFailure(Exception exception, string correlationId) => exception switch
    {
        CustomerAccountNotFoundException => Results.NotFound(),
        CustomerAccountValidationException validation => Results.Problem(validation.Message, statusCode: 400),
        CustomerAccountConflictException conflict => Conflict(conflict.Code),
        LegacyWriteNotConfiguredException => Unavailable(correlationId),
        LegacyWriteUnavailableException => Unavailable(correlationId),
        _ => throw exception,
    };

    private static IResult Conflict(string code) { var problem = new ProblemDetails { Title = "Account update conflict.", Status = 409 }; problem.Extensions["code"] = code; return Results.Conflict(problem); }
    private static IResult Unavailable(string correlationId) => Results.Problem(new ProblemDetails { Title = "The Legacy write service is unavailable.", Status = 503, Extensions = { ["correlationId"] = correlationId } });
    private static IResult InvalidPersonId() => Results.ValidationProblem(new Dictionary<string, string[]> { ["personId"] = ["personId must be greater than zero."] });
}

public sealed record CustomerAccountRequest(short BankId, short BranchNumber, byte CurrencyCode, byte AccountTypeCode, string? AccountNumber, string? Clabe, DateTime? ExpectedModifiedAt)
{
    public CustomerAccountCreateCommand ToCreateCommand(int personId) => new(personId, BankId, BranchNumber, CurrencyCode, AccountTypeCode, AccountNumber ?? throw new CustomerAccountValidationException("Account number is required."), Clabe ?? throw new CustomerAccountValidationException("CLABE is required."));
    public CustomerAccountUpdateCommand ToUpdateCommand(int personId, int accountId) => new(personId, accountId, BankId, BranchNumber, CurrencyCode, AccountTypeCode, AccountNumber, Clabe, ExpectedModifiedAt ?? throw new CustomerAccountValidationException("expectedModifiedAt is required."));
}

public sealed record AccountStateChangeRequest(DateTime? ExpectedModifiedAt);
