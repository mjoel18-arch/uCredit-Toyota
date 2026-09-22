using Microsoft.AspNetCore.Mvc;
using UCredit.Api.Contracts;
using UCredit.Api.Security;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Endpoints;

public static class CustomerEmailEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEmailEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/customers/{personId:int}/emails", GetAsync)
            .WithTags("Customers").RequireAuthorization("customers.read");
        endpoints.MapGet("/api/v1/catalogs/email-uses", GetUsagesAsync)
            .WithTags("Customers").RequireAuthorization("customers.read");

        var group = endpoints.MapGroup("/api/v1/customers/{personId:int}/emails")
            .WithTags("Customers").RequireAuthorization("customers.write")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        group.MapPost("", CreateAsync);
        group.MapPut("{emailId:int}", UpdateAsync);
        group.MapPost("{emailId:int}/activate", ActivateAsync);
        group.MapPost("{emailId:int}/deactivate", DeactivateAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(int personId, [FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] ICustomerEmailReadRepository repository, CancellationToken cancellationToken)
    {
        if (personId <= 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["personId"] = ["personId must be greater than zero."] });
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        var emails = await repository.GetByPersonIdAsync(personId, cancellationToken);
        return emails is null ? Results.NotFound() : Results.Ok(emails.Select(ManagedCustomerEmailResponse.FromModel).ToArray());
    }

    private static async Task<IResult> GetUsagesAsync([FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] ICustomerEmailUsageRepository repository, CancellationToken cancellationToken)
    {
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        var usages = await repository.GetActiveUsagesAsync(cancellationToken);
        return Results.Ok(usages.Select(usage => new CustomerEmailUsageResponse(usage.Code, usage.Description)).ToArray());
    }

    private static Task<IResult> CreateAsync(int personId, [FromBody] CustomerEmailRequest request, [FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] IExecutionActorContext actorContext, [FromServices] ICustomerEmailWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken) =>
        MutateAsync(personId, null, request, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);

    private static Task<IResult> UpdateAsync(int personId, int emailId, [FromBody] CustomerEmailRequest request, [FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] IExecutionActorContext actorContext, [FromServices] ICustomerEmailWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken) =>
        MutateAsync(personId, emailId, request, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);

    private static async Task<IResult> MutateAsync(int personId, int? emailId, CustomerEmailRequest request, IExecutionTenantContext tenantContext, IConfiguration configuration, IExecutionActorContext actorContext, ICustomerEmailWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (personId <= 0 || emailId is <= 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["personId"] = ["personId must be greater than zero."] });
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var actor = await actorContext.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(actor.LegacyUserCode)) return Results.UnprocessableEntity(new ProblemDetails { Title = "Legacy user code is required.", Status = 422 });
            ManagedCustomerEmail result = emailId is null
                ? await repository.CreateAsync(request.ToCreateCommand(personId), actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken)
                : await repository.UpdateAsync(request.ToUpdateCommand(personId, emailId.Value), actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return emailId is null
                ? Results.Created($"/api/v1/customers/{personId}/emails/{result.EmailId}", ManagedCustomerEmailResponse.FromModel(result))
                : Results.Ok(ManagedCustomerEmailResponse.FromModel(result));
        }
        catch (Exception exception) { return MapFailure(exception, httpContext.TraceIdentifier); }
    }

    private static Task<IResult> ActivateAsync(int personId, int emailId, [FromBody] EmailStateChangeRequest request, [FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] IExecutionActorContext actorContext, [FromServices] ICustomerEmailWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken) => ChangeStateAsync(personId, emailId, request, true, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);
    private static Task<IResult> DeactivateAsync(int personId, int emailId, [FromBody] EmailStateChangeRequest request, [FromServices] IExecutionTenantContext tenantContext, [FromServices] IConfiguration configuration, [FromServices] IExecutionActorContext actorContext, [FromServices] ICustomerEmailWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken) => ChangeStateAsync(personId, emailId, request, false, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);

    private static async Task<IResult> ChangeStateAsync(int personId, int emailId, EmailStateChangeRequest request, bool activate, IExecutionTenantContext tenantContext, IConfiguration configuration, IExecutionActorContext actorContext, ICustomerEmailWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (personId <= 0 || emailId <= 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["personId"] = ["personId must be greater than zero."] });
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var actor = await actorContext.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(actor.LegacyUserCode)) return Results.UnprocessableEntity(new ProblemDetails { Title = "Legacy user code is required.", Status = 422 });
            var command = new CustomerEmailStateChangeCommand(personId, emailId, request.ExpectedModifiedAt ?? throw new CustomerEmailValidationException("expectedModifiedAt is required."));
            var result = activate
                ? await repository.ActivateAsync(command, actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken)
                : await repository.DeactivateAsync(command, actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok(ManagedCustomerEmailResponse.FromModel(result));
        }
        catch (Exception exception) { return MapFailure(exception, httpContext.TraceIdentifier); }
    }

    private static async Task<bool> IsDeploymentTenantAsync(IExecutionTenantContext tenantContext, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var tenant = await tenantContext.GetAsync(cancellationToken);
        return tenant is not null && !string.IsNullOrWhiteSpace(configuration["Deployment:TenantCode"]) && string.Equals(tenant.TenantCode, configuration["Deployment:TenantCode"], StringComparison.Ordinal);
    }

    private static IResult MapFailure(Exception exception, string correlationId) => exception switch
    {
        CustomerEmailNotFoundException => Results.NotFound(),
        CustomerEmailValidationException validation => Results.Problem(validation.Message, statusCode: 400),
        CustomerEmailConflictException conflict => Conflict(conflict.Code),
        LegacyWriteNotConfiguredException or LegacyWriteUnavailableException => Results.Problem(new ProblemDetails { Title = "The Legacy write service is unavailable.", Status = 503, Extensions = { ["correlationId"] = correlationId } }),
        _ => throw exception,
    };

    private static IResult Conflict(string code)
    {
        var problem = new ProblemDetails { Title = "Email update conflict.", Status = 409 };
        problem.Extensions["code"] = code;
        return Results.Conflict(problem);
    }
}

public sealed record CustomerEmailRequest(string? Contact, string? Email, IReadOnlyList<int>? UsageCodes, DateTime? ExpectedModifiedAt)
{
    public CustomerEmailCreateCommand ToCreateCommand(int personId) => new(personId, Contact, Email ?? throw new CustomerEmailValidationException("Email is required."), UsageCodes ?? []);
    public CustomerEmailUpdateCommand ToUpdateCommand(int personId, int emailId) => new(personId, emailId, Contact, Email, UsageCodes ?? [], ExpectedModifiedAt ?? throw new CustomerEmailValidationException("expectedModifiedAt is required."));
}

public sealed record EmailStateChangeRequest(DateTime? ExpectedModifiedAt);
