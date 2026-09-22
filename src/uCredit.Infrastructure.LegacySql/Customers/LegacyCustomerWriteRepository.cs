using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

public sealed class CustomerCreateConflictException(string message) : Exception(message);
public enum LegacyWriteConfigurationReason
{
    NotDevelopment,
    MissingWriteConnection,
    WriteTestsNotAllowed,
    MissingExpectedDatabase,
    InvalidExpectedDatabase
}

public sealed class LegacyWriteNotConfiguredException(string message, string stage, LegacyWriteConfigurationReason reason) : Exception(message)
{
    public string Stage { get; } = stage;
    public LegacyWriteConfigurationReason Reason { get; } = reason;
}

public sealed class LegacyWriteUnavailableException(string stage, Exception innerException) : Exception("Legacy write service unavailable.", innerException)
{
    public string Stage { get; } = stage;
}

internal sealed class LegacyPersonPepChecker : IPersonPepChecker
{
    public Task<PepCheckResult> CheckAsync(CustomerCreateCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PepCheckResult(PepCheckStatus.Unavailable));
}

public sealed partial class LegacyCustomerWriteRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext tenantContext,
    IHostEnvironment hostEnvironment,
    IConfiguration configuration,
    ILogger<LegacyCustomerWriteRepository> logger) : ICustomerWriteRepository, ICustomerAddressWriteRepository
{
    internal const string AddressStage = "address";
    internal const string TaxRegimeLookupSql = "SELECT TOP (1) RFI_CL_CLAVE FROM dbo.CREGIMEN_FISCAL WHERE RFI_CL_CLAVE = @TaxRegimeCode AND RFI_CL_PJURIDICA = @FiscalPersonality AND RFI_FG_STATUS = 1;";
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<LegacyCustomerWriteRepository> _logger = logger;

    public static LegacyWriteConfigurationReason? GetConfigurationReason(LegacySqlOptions options, bool isDevelopment)
    {
        if (!isDevelopment) return LegacyWriteConfigurationReason.NotDevelopment;
        if (string.IsNullOrWhiteSpace(options.WriteConnectionString)) return LegacyWriteConfigurationReason.MissingWriteConnection;
        if (!options.AllowLegacyWriteTests) return LegacyWriteConfigurationReason.WriteTestsNotAllowed;
        if (string.IsNullOrWhiteSpace(options.LegacyWriteTestDatabase)) return LegacyWriteConfigurationReason.MissingExpectedDatabase;
        if (!string.Equals(options.LegacyWriteTestDatabase, "pr_t", StringComparison.Ordinal)) return LegacyWriteConfigurationReason.InvalidExpectedDatabase;
        return null;
    }

    public async Task<CustomerCreateResult> CreateAsync(CustomerCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyUserCode) || legacyUserCode.Trim().Length > 8)
            throw new ArgumentException("A valid LegacyUserCode is required.", nameof(legacyUserCode));
        if (CustomerCreateValidator.Validate(command).Count > 0)
            throw new ArgumentException("The customer create command is invalid.", nameof(command));

        var tenant = await _tenantContext.GetAsync(cancellationToken) ?? throw new InvalidOperationException("No execution tenant is selected.");
        if (tenant.AllowedCompanyIds.Count == 0)
            throw new InvalidOperationException("The execution tenant has no Legacy company scope.");
        EnsureWriteConfigured();

        await using var connection = new SqlConnection(_options.WriteConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (SqlException exception)
        {
            throw new LegacyWriteUnavailableException("connection", exception);
        }
        try
        {
            await EnsureTestDatabaseAsync(connection, cancellationToken);
        }
        catch (SqlException exception)
        {
            throw new LegacyWriteUnavailableException("database_guard", exception);
        }
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var stage = "rfc";
        try
        {
            stage = "catalogs";
            var taxRegime = await ValidateTaxRegimeAsync(connection, transaction, command, cancellationToken);
            await EnsureRfcAvailableAsync(connection, transaction, command.Rfc.Trim(), cancellationToken);
            stage = "consecutive";
            var personId = await NextIdAsync(connection, transaction, "CPERSONA", cancellationToken);
            var phoneId = await NextIdAsync(connection, transaction, "CTELEFONO", cancellationToken);
            var bitacoraId = await NextIdAsync(connection, transaction, "KBITACORA", cancellationToken);
            var fullName = command.LegalPersonality == CustomerCreatePersonality.Moral
                ? command.LegalName!.Trim()
                : string.Join(" ", new[] { command.PaternalSurname, command.MaternalSurname, command.FirstName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
            var operationDate = DateTime.UtcNow;
            var parameters = new DynamicParameters();
            parameters.Add("PersonId", personId, DbType.Int32); parameters.Add("Personality", (int)command.LegalPersonality, DbType.Int32);
            parameters.Add("Rfc", command.Rfc.Trim(), DbType.String, size: 13); parameters.Add("Name", fullName, DbType.String, size: 200);
            parameters.Add("FirstName", command.FirstName, DbType.String, size: 100); parameters.Add("PaternalSurname", command.PaternalSurname, DbType.String, size: 100); parameters.Add("MaternalSurname", command.MaternalSurname, DbType.String, size: 100);
            parameters.Add("LegalName", command.LegalName, DbType.String, size: 200); parameters.Add("CapitalRegime", command.CapitalRegime, DbType.String, size: 200);
            parameters.Add("ContactForm", command.ContactFormCode, DbType.Int32); parameters.Add("GroupCode", command.GroupCode, DbType.Int32); parameters.Add("RiskCode", command.RiskCode, DbType.Int32); parameters.Add("TaxRegime", taxRegime, DbType.Int32); parameters.Add("CountryCode", command.CountryCode, DbType.Int32);
            parameters.Add("OperationDate", operationDate, DbType.DateTime); parameters.Add("ConstitutionOrBirthDate", command.ConstitutionOrBirthDate.ToDateTime(TimeOnly.MinValue), DbType.Date); parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8);
            parameters.Add("PhoneId", phoneId, DbType.Int32); parameters.Add("PhoneType", command.PhoneTypeCode, DbType.Int32); parameters.Add("UnassociatedAddressId", 0, DbType.Int32); parameters.Add("AreaCode", command.AreaCode.Trim(), DbType.String, size: 10); parameters.Add("PhoneNumber", command.PhoneNumber.Trim(), DbType.String, size: 30); parameters.Add("PhoneExtension", command.PhoneExtension, DbType.String, size: 10); parameters.Add("PhoneContact", command.PhoneContact, DbType.String, size: 200);
            parameters.Add("BitacoraId", bitacoraId, DbType.Int32);

            stage = "person";
            await connection.ExecuteAsync(new CommandDefinition(CreatePersonInsertSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            stage = "subtype";
            await connection.ExecuteAsync(new CommandDefinition(command.LegalPersonality == CustomerCreatePersonality.Moral ? CreateMoralInsertSql : CreatePhysicalInsertSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            stage = "role";
            await connection.ExecuteAsync(new CommandDefinition(CreateRoleInsertSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            stage = "phone";
            await connection.ExecuteAsync(new CommandDefinition(CreatePhoneInsertSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            stage = "audit";
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO dbo.KBITACORA (BIT_FL_CVE, BIT_FE_FECHA, ATV_FL_CVE, BIT_FE_OPERACION, BIT_DS_REFERENCIA, USR_CL_CVE, USR_CL_FIRMA, BIT_TOP_CVE) VALUES (@BitacoraId, SYSUTCDATETIME(), 4, @OperationDate, @Reference, @LegacyUserCode, @LegacyUserCode, '');", new { BitacoraId = bitacoraId, OperationDate = operationDate, Reference = $"Se agrego la persona con clave {personId}", LegacyUserCode = legacyUserCode.Trim() }, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            stage = "commit";
            await transaction.CommitAsync(cancellationToken);
            return new CustomerCreateResult(personId);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new CustomerCreateConflictException("The customer already exists or conflicts with Legacy data.");
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            LogFailure(_logger, exception.GetType().Name, stage, exception is SqlException sqlException ? sqlException.Number : null, correlationId);
            throw;
        }
    }

    [LoggerMessage(EventId = 4201, Level = LogLevel.Error, Message = "Customer creation failed. ExceptionType={ExceptionType} SqlNumber={SqlNumber} Stage={Stage} CorrelationId={CorrelationId}")]
    private static partial void LogFailure(ILogger logger, string exceptionType, string stage, int? sqlNumber, string correlationId);

    internal const string CreatePersonInsertSql = """
        INSERT INTO dbo.CPERSONA (PNA_FL_PERSONA, PNA_CL_PJURIDICA, PNA_CL_RFC, PNA_DS_NOMBRE, PNA_FG_FCONTACTO, GPR_FL_CVE, PNA_FE_ALTA, PNA_FE_ULTMOD, PNA_FG_STATUS, USR_CL_CVE, PNA_CL_TCARTERA, PNA_CL_REFPAGO, GRI_FL_CVE, PAI_FL_CVE, PNA_NO_CODE, PNA_FG_FRONTERIZO, RFI_CL_CLAVE)
        VALUES (@PersonId, @Personality, @Rfc, @Name, @ContactForm, @GroupCode, @OperationDate, @OperationDate, 1, @LegacyUserCode, 1, 2, @RiskCode, @CountryCode, 0, 0, @TaxRegime);
        """;

    internal const string CreateMoralInsertSql = """
        INSERT INTO dbo.CPMORAL (PNA_FL_PERSONA, PMO_DS_RAZON_SOCIAL, PMO_DS_REGIMEN_CAPITAL, PMO_FE_CONSTITUCION, PMO_FE_ULTMOD, USR_CL_CVE)
        VALUES (@PersonId, @LegalName, @CapitalRegime, @ConstitutionOrBirthDate, @OperationDate, @LegacyUserCode);
        """;

    internal const string CreatePhysicalInsertSql = """
        INSERT INTO dbo.CPFISICA (PNA_FL_PERSONA, PFI_DS_APATERNO, PFI_DS_AMATERNO, PFI_DS_NOMBRE, PFI_FE_NACIMIENTO, PFI_FE_ULTMOD, USR_CL_CVE)
        VALUES (@PersonId, @PaternalSurname, @MaternalSurname, @FirstName, @ConstitutionOrBirthDate, @OperationDate, @LegacyUserCode);
        """;

    internal const string CreateRoleInsertSql = """
        INSERT INTO dbo.CPTIPO (PNA_FL_PERSONA, PTI_FG_VALOR, PTI_FE_ULTMOD, USR_CL_CVE)
        VALUES (@PersonId, 1, @OperationDate, @LegacyUserCode);
        """;

    internal const string CreatePhoneInsertSql = """
        INSERT INTO dbo.CTELEFONO (TFN_FL_CVE, PNA_FL_PERSONA, TTL_FL_CVE, DMO_FL_CVE, TFN_CL_LARGA_DISTANCIA, TFN_CL_TELEFONO, TFN_CL_EXTENSION, TFN_FG_STATUS, TFN_FG_REGDEFAULT, TFN_FE_ULTMOD, USR_CL_CVE, TFN_CL_LADA, TFN_DS_CONTACTO)
        VALUES (@PhoneId, @PersonId, @PhoneType, @UnassociatedAddressId, '', @PhoneNumber, @PhoneExtension, 1, 1, @OperationDate, @LegacyUserCode, @AreaCode, @PhoneContact);
        """;

    internal static string NormalizeOptionalLegacyString(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static async Task EnsureRfcAvailableAsync(SqlConnection connection, SqlTransaction transaction, string rfc, CancellationToken cancellationToken)
    {
        var existing = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT TOP (1) PNA_FL_PERSONA FROM dbo.CPERSONA WITH (UPDLOCK, HOLDLOCK) WHERE PNA_CL_RFC = @Rfc;", new { Rfc = rfc }, transaction, cancellationToken: cancellationToken));
        if (existing is not null) throw new CustomerCreateConflictException("The customer RFC already exists.");
    }

    private static async Task<int> ValidateTaxRegimeAsync(SqlConnection connection, SqlTransaction transaction, CustomerCreateCommand command, CancellationToken cancellationToken)
    {
        var fiscalPersonality = command.LegalPersonality switch
        {
            CustomerCreatePersonality.Individual or CustomerCreatePersonality.IndividualBusiness => 1,
            CustomerCreatePersonality.Moral => 2,
            _ => throw new CustomerCreateValidationException("The legal personality is not compatible with a tax regime.")
        };
        var taxRegimeCode = command.TaxRegimeCode.Trim();
        var matchedKey = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            TaxRegimeLookupSql,
            new { TaxRegimeCode = taxRegimeCode, FiscalPersonality = fiscalPersonality }, transaction, cancellationToken: cancellationToken));
        if (!int.TryParse(matchedKey, out var taxRegime))
            throw new CustomerCreateValidationException("The tax regime is not active or compatible with the legal personality.");
        return taxRegime;
    }

    private async Task<int> NextIdAsync(SqlConnection connection, SqlTransaction transaction, string tableName, CancellationToken cancellationToken)
    {
        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("UPDATE dbo.CCATCONSEC WITH (UPDLOCK, HOLDLOCK) SET CCT_NO_CONSECUTIVO = CCT_NO_CONSECUTIVO + 1 OUTPUT INSERTED.CCT_NO_CONSECUTIVO WHERE EMP_FL_CVE = 0 AND CCS_DS_NOMTABLA = @TableName;", new { TableName = tableName }, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
        return id ?? throw new InvalidOperationException($"No Legacy consecutive configured for {tableName}.");
    }

    private void EnsureWriteConfigured()
    {
        var reason = GetConfigurationReason(_options, _hostEnvironment.IsDevelopment());
        if (reason is not null)
            throw new LegacyWriteNotConfiguredException("Legacy write configuration is not valid.", "configuration", reason.Value);
    }

    private static async Task EnsureTestDatabaseAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var database = await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT DB_NAME();", cancellationToken: cancellationToken));
        if (!string.Equals(database, "pr_t", StringComparison.Ordinal)) throw new LegacyWriteNotConfiguredException("Legacy write test database is not pr_t.", "database_guard", LegacyWriteConfigurationReason.InvalidExpectedDatabase);
    }
}
