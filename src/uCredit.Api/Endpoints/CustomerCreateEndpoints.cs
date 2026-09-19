using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UCredit.Api.Security;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Endpoints;

public static class CustomerCreateEndpoints
{
    public static IEndpointRouteBuilder MapCustomerCreateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/customers", CreateAsync)
            .WithTags("Customers")
            .RequireAuthorization("customers.write")
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .WithMetadata(new RequestSizeLimitAttribute(32 * 1024));
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CustomerCreateRequest request,
        IOptions<CustomerCreationOptions> options,
        IEnumerable<IPersonPepChecker> pepCheckers,
        ICustomerWriteRepository repository,
        IExecutionActorContext actorContext,
        CancellationToken cancellationToken)
    {
        var command = request.ToCommand();
        var errors = CustomerCreateValidator.Validate(command);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var actor = await actorContext.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(actor.LegacyUserCode))
            return Results.UnprocessableEntity(new ProblemDetails { Title = "Legacy user code is required.", Status = StatusCodes.Status422UnprocessableEntity });

        var pepStatus = "NotExecuted";
        if (options.Value.RequirePepCheck)
        {
            var checker = pepCheckers.SingleOrDefault();
            if (checker is null) return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The PEP service is unavailable.");
            PepCheckResult pep;
            try
            {
                pep = await checker.CheckAsync(command, cancellationToken);
            }
            catch (LegacyWriteNotConfiguredException)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The PEP service is unavailable.");
            }
            if (pep.Status == PepCheckStatus.Unavailable)
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The PEP service is unavailable.");
            if (pep.Status == PepCheckStatus.Match && !request.PepConfirmed)
            {
                var problem = new ProblemDetails { Title = "PEP review confirmation is required before creating the customer.", Status = StatusCodes.Status422UnprocessableEntity };
                problem.Extensions["code"] = "pep_confirmation_required";
                return Results.UnprocessableEntity(problem);
            }
            pepStatus = "Executed";
        }

        try
        {
            var result = await repository.CreateAsync(command, actor.LegacyUserCode, cancellationToken);
            return Results.Created($"/api/v1/customers/{result.PersonId}", new CustomerCreateResponse(result.PersonId, pepStatus));
        }
        catch (CustomerCreateConflictException exception)
        {
            return Results.Conflict(new ProblemDetails { Title = "The customer already exists or conflicts with Legacy data.", Detail = exception.Message, Status = StatusCodes.Status409Conflict });
        }
        catch (LegacyWriteNotConfiguredException)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The Legacy write service is unavailable.");
        }
    }
}

public sealed record CustomerCreateRequest(
    CustomerCreatePersonality LegalPersonality, string Rfc, string? FirstName, string? PaternalSurname, string? MaternalSurname,
    string? LegalName, string? CapitalRegime, DateOnly ConstitutionOrBirthDate, int CountryCode, int GroupCode, int RiskCode,
    int ContactFormCode, int TaxRegimeCode, int AddressTypeCode, string PostalCode, string State, string City, string Municipality,
    string Neighborhood, string StreetAndNumber, string ExteriorNumber, string? InteriorNumber, string? AddressReference,
    string? AddressSchedule, int AddressStatusCode, int PhoneTypeCode, string AreaCode, string PhoneNumber, string? PhoneExtension,
    string? PhoneContact, string EmailContact, string Email, IReadOnlyList<int> EmailUsageCodes, bool PepConfirmed)
{
    public CustomerCreateCommand ToCommand() => new(LegalPersonality, Rfc, FirstName, PaternalSurname, MaternalSurname, LegalName, CapitalRegime, ConstitutionOrBirthDate, CountryCode, GroupCode, RiskCode, ContactFormCode, TaxRegimeCode, AddressTypeCode, PostalCode, State, City, Municipality, Neighborhood, StreetAndNumber, ExteriorNumber, InteriorNumber, AddressReference, AddressSchedule, AddressStatusCode, PhoneTypeCode, AreaCode, PhoneNumber, PhoneExtension, PhoneContact, EmailContact, Email, EmailUsageCodes ?? []);
}

public sealed record CustomerCreateResponse(int PersonId, string PepValidationStatus);
