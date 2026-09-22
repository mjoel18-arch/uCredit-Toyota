using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using UCredit.Api.Security;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Endpoints;

public static class CustomerCreateEndpoints
{
    private static readonly Action<ILogger, string, string, string, string, Exception?> UnavailableLog =
        LoggerMessage.Define<string, string, string, string>(LogLevel.Warning, new EventId(4202), "Customer creation unavailable. ExceptionType={ExceptionType} Stage={Stage} Reason={Reason} CorrelationId={CorrelationId}");
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
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CustomerCreate");
        var correlationId = httpContext.TraceIdentifier;
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
            if (checker is null)
            {
                LogUnavailable(logger, "configuration", typeof(LegacyWriteNotConfiguredException), "none", correlationId);
                return Unavailable("The PEP service is unavailable.", correlationId);
            }
            PepCheckResult pep;
            try
            {
                pep = await checker.CheckAsync(command, cancellationToken);
            }
            catch (LegacyWriteNotConfiguredException exception)
            {
                LogUnavailable(logger, exception.Stage, exception.GetType(), exception.Reason.ToString(), correlationId);
                return Unavailable("The PEP service is unavailable.", correlationId);
            }
            if (pep.Status == PepCheckStatus.Unavailable)
            {
                LogUnavailable(logger, "configuration", typeof(LegacyWriteNotConfiguredException), "none", correlationId);
                return Unavailable("The PEP service is unavailable.", correlationId);
            }
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
            var result = await repository.CreateAsync(command, actor.LegacyUserCode, correlationId, cancellationToken);
            return Results.Created($"/api/v1/customers/{result.PersonId}", new CustomerCreateResponse(result.PersonId, pepStatus));
        }
        catch (CustomerCreateConflictException exception)
        {
            return Results.Conflict(new ProblemDetails { Title = "The customer already exists or conflicts with Legacy data.", Detail = exception.Message, Status = StatusCodes.Status409Conflict });
        }
        catch (CustomerCreateValidationException exception)
        {
            return Results.BadRequest(new ProblemDetails { Title = "The customer data is invalid.", Detail = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
        catch (LegacyWriteNotConfiguredException exception)
        {
            LogUnavailable(logger, exception.Stage, exception.GetType(), exception.Reason.ToString(), correlationId);
            return Unavailable("The Legacy write service is unavailable.", correlationId);
        }
        catch (LegacyWriteUnavailableException exception)
        {
            LogUnavailable(logger, exception.Stage, exception.InnerException?.GetType() ?? exception.GetType(), "none", correlationId);
            return Unavailable("The Legacy write service is unavailable.", correlationId);
        }
    }

    private static IResult Unavailable(string title, string correlationId)
    {
        var problem = new ProblemDetails { Title = title, Status = StatusCodes.Status503ServiceUnavailable };
        problem.Extensions["correlationId"] = correlationId;
        return Results.Problem(problem);
    }

    private static void LogUnavailable(ILogger logger, string stage, Type exceptionType, string reason, string correlationId) =>
        UnavailableLog(logger, exceptionType.Name, stage, reason, correlationId, null);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CustomerCreateRequest(
    CustomerCreatePersonality LegalPersonality, string Rfc, string? FirstName, string? PaternalSurname, string? MaternalSurname,
    string? LegalName, string? CapitalRegime, DateOnly ConstitutionOrBirthDate, int CountryCode, int GroupCode, int RiskCode,
    int ContactFormCode, string TaxRegimeCode, int PhoneTypeCode, string AreaCode, string PhoneNumber, string? PhoneExtension,
    string? PhoneContact, bool PepConfirmed)
{
    public CustomerCreateCommand ToCommand() => new(LegalPersonality, Rfc, FirstName, PaternalSurname, MaternalSurname, LegalName, CapitalRegime, ConstitutionOrBirthDate, CountryCode, GroupCode, RiskCode, ContactFormCode, TaxRegimeCode, PhoneTypeCode, AreaCode, PhoneNumber, PhoneExtension, PhoneContact);
}

public sealed record CustomerCreateResponse(int PersonId, string PepValidationStatus);
