using Microsoft.AspNetCore.Mvc;
using UCredit.Api.Contracts;
using UCredit.Api.Security;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Endpoints;

public static class CustomerPhoneEndpoints
{
    public static IEndpointRouteBuilder MapCustomerPhoneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/customers/{personId:int}/phones", GetAsync)
            .WithTags("Customers")
            .RequireAuthorization("customers.read");

        var group = endpoints.MapGroup("/api/v1/customers/{personId:int}/phones")
            .WithTags("Customers")
            .RequireAuthorization("customers.write")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        group.MapPost("", CreateAsync);
        group.MapPut("{phoneId:int}", UpdateAsync);
        group.MapPost("{phoneId:int}/activate", ActivateAsync);
        group.MapPost("{phoneId:int}/deactivate", DeactivateAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        int personId,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] ICustomerPhoneReadRepository repository,
        CancellationToken cancellationToken)
    {
        if (personId <= 0) return InvalidPersonId();
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        var phones = await repository.GetByPersonIdAsync(personId, cancellationToken);
        return phones is null ? Results.NotFound() : Results.Ok(phones.Select(ManagedCustomerPhoneResponse.FromModel).ToArray());
    }

    private static async Task<IResult> CreateAsync(
        int personId,
        [FromBody] CustomerPhoneRequest request,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IExecutionActorContext actorContext,
        [FromServices] ICustomerPhoneWriteRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (personId <= 0) return InvalidPersonId();
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var actor = await actorContext.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(actor.LegacyUserCode)) return MissingLegacyUserCode();
            var result = await repository.CreateAsync(request.ToCreateCommand(personId), actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return Results.Created($"/api/v1/customers/{personId}/phones/{result.PhoneId}", ManagedCustomerPhoneResponse.FromModel(result));
        }
        catch (Exception exception) { return MapFailure(exception, httpContext.TraceIdentifier); }
    }

    private static async Task<IResult> UpdateAsync(
        int personId,
        int phoneId,
        [FromBody] CustomerPhoneRequest request,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IExecutionActorContext actorContext,
        [FromServices] ICustomerPhoneWriteRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (personId <= 0 || phoneId <= 0) return InvalidPersonId();
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var actor = await actorContext.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(actor.LegacyUserCode)) return MissingLegacyUserCode();
            var result = await repository.UpdateAsync(request.ToUpdateCommand(personId, phoneId), actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok(ManagedCustomerPhoneResponse.FromModel(result));
        }
        catch (Exception exception) { return MapFailure(exception, httpContext.TraceIdentifier); }
    }

    private static Task<IResult> ActivateAsync(
        int personId, int phoneId, [FromBody] PhoneStateChangeRequest request,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IExecutionActorContext actorContext,
        [FromServices] ICustomerPhoneWriteRepository repository,
        HttpContext httpContext, CancellationToken cancellationToken) =>
        ChangeStateAsync(personId, phoneId, request, true, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);

    private static Task<IResult> DeactivateAsync(
        int personId, int phoneId, [FromBody] PhoneStateChangeRequest request,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IExecutionActorContext actorContext,
        [FromServices] ICustomerPhoneWriteRepository repository,
        HttpContext httpContext, CancellationToken cancellationToken) =>
        ChangeStateAsync(personId, phoneId, request, false, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);

    private static async Task<IResult> ChangeStateAsync(int personId, int phoneId, PhoneStateChangeRequest request, bool activate,
        IExecutionTenantContext tenantContext, IConfiguration configuration, IExecutionActorContext actorContext,
        ICustomerPhoneWriteRepository repository, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (personId <= 0 || phoneId <= 0) return InvalidPersonId();
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var actor = await actorContext.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(actor.LegacyUserCode)) return MissingLegacyUserCode();
            var command = new CustomerPhoneStateChangeCommand(personId, phoneId, request.ExpectedModifiedAtValue, request.ReplacementPhoneId);
            var result = activate
                ? await repository.ActivateAsync(command, actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken)
                : await repository.DeactivateAsync(command, actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok(ManagedCustomerPhoneResponse.FromModel(result));
        }
        catch (Exception exception) { return MapFailure(exception, httpContext.TraceIdentifier); }
    }

    private static async Task<bool> IsDeploymentTenantAsync(IExecutionTenantContext tenantContext, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var tenant = await tenantContext.GetAsync(cancellationToken);
        var deploymentTenant = configuration["Deployment:TenantCode"];
        return tenant is not null && !string.IsNullOrWhiteSpace(deploymentTenant) && string.Equals(tenant.TenantCode, deploymentTenant, StringComparison.Ordinal);
    }

    private static IResult MapFailure(Exception exception, string correlationId) => exception switch
    {
        CustomerPhoneNotFoundException => Results.NotFound(),
        CustomerPhoneValidationException validation => Results.Problem(validation.Message, statusCode: StatusCodes.Status400BadRequest),
        CustomerPhoneConflictException conflict => Conflict(conflict.Code),
        LegacyWriteNotConfiguredException => Unavailable(correlationId),
        LegacyWriteUnavailableException => Unavailable(correlationId),
        _ => throw exception,
    };

    private static IResult Conflict(string code)
    {
        var problem = new ProblemDetails { Title = "Phone update conflict.", Status = StatusCodes.Status409Conflict };
        problem.Extensions["code"] = code;
        return Results.Conflict(problem);
    }

    private static IResult Unavailable(string correlationId) => Results.Problem(new ProblemDetails
    {
        Title = "The Legacy write service is unavailable.",
        Status = StatusCodes.Status503ServiceUnavailable,
        Extensions = { ["correlationId"] = correlationId },
    });

    private static IResult InvalidPersonId() => Results.ValidationProblem(new Dictionary<string, string[]> { ["personId"] = ["personId must be greater than zero."] });
    private static IResult MissingLegacyUserCode() => Results.UnprocessableEntity(new ProblemDetails { Title = "Legacy user code is required.", Status = StatusCodes.Status422UnprocessableEntity });
}

public sealed record CustomerPhoneRequest(
    int PhoneTypeCode,
    string? LongDistanceCode,
    string? AreaCode,
    string PhoneNumber,
    string? Extension,
    string? ContactName,
    bool IsDefault,
    DateTime? ExpectedModifiedAt)
{
    public CustomerPhoneCreateCommand ToCreateCommand(int personId) => new(personId, PhoneTypeCode, LongDistanceCode, AreaCode, PhoneNumber, Extension, ContactName, IsDefault);
    public CustomerPhoneUpdateCommand ToUpdateCommand(int personId, int phoneId) => new(personId, phoneId, PhoneTypeCode, LongDistanceCode, AreaCode, PhoneNumber, Extension, ContactName, IsDefault, ExpectedModifiedAt ?? throw new CustomerPhoneValidationException("expectedModifiedAt is required."));
}

public sealed record PhoneStateChangeRequest(DateTime? ExpectedModifiedAt, int? ReplacementPhoneId)
{
    public DateTime ExpectedModifiedAtValue => ExpectedModifiedAt ?? throw new CustomerPhoneValidationException("expectedModifiedAt is required.");
}
