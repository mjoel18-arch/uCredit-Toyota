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

public sealed partial class LegacyCustomerPhoneWriteRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext tenantContext,
    IHostEnvironment hostEnvironment,
    IConfiguration configuration,
    ILogger<LegacyCustomerPhoneWriteRepository> logger) : ICustomerPhoneWriteRepository
{
    internal sealed class LegacyCustomerPhoneRow
    {
        public int PhoneId { get; init; }
        public int PersonId { get; init; }
        public byte PhoneTypeCode { get; init; }
        public int? AddressId { get; init; }
        public string? LongDistanceCode { get; init; }
        public string? AreaCode { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Extension { get; init; }
        public byte StatusCode { get; init; }
        public string? InactiveReason { get; init; }
        public byte IsDefault { get; init; }
        public DateTime? ModifiedAt { get; init; }
        public string? ContactName { get; init; }
    }

    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;
    private readonly ILogger<LegacyCustomerPhoneWriteRepository> _logger = logger;

    internal const string PhoneSelectSql = """
        SELECT T.TFN_FL_CVE AS PhoneId,
               T.PNA_FL_PERSONA AS PersonId,
               T.TTL_FL_CVE AS PhoneTypeCode,
               T.DMO_FL_CVE AS AddressId,
               T.TFN_CL_LARGA_DISTANCIA AS LongDistanceCode,
               T.TFN_CL_LADA AS AreaCode,
               T.TFN_CL_TELEFONO AS PhoneNumber,
               T.TFN_CL_EXTENSION AS Extension,
               T.TFN_FG_STATUS AS StatusCode,
               T.TFN_DS_RAZON_INACTIVO AS InactiveReason,
               T.TFN_FG_REGDEFAULT AS IsDefault,
               T.TFN_FE_ULTMOD AS ModifiedAt,
               T.TFN_DS_CONTACTO AS ContactName
        FROM dbo.CTELEFONO AS T
        WHERE T.PNA_FL_PERSONA = @PersonId AND T.TFN_FL_CVE = @PhoneId;
        """;

    internal const string PhoneInsertSql = """
        INSERT INTO dbo.CTELEFONO
        (TFN_FL_CVE, TTL_FL_CVE, DMO_FL_CVE, TFN_CL_LARGA_DISTANCIA, TFN_CL_LADA, TFN_CL_TELEFONO, TFN_CL_EXTENSION, TFN_FG_STATUS, TFN_DS_RAZON_INACTIVO, TFN_FG_REGDEFAULT, TFN_FE_ULTMOD, USR_CL_CVE, PNA_FL_PERSONA, TFN_DS_CONTACTO)
        VALUES
        (@PhoneId, @PhoneTypeCode, @AddressId, @LongDistanceCode, @AreaCode, @PhoneNumber, @Extension, 1, NULL, @IsDefault, @OperationDate, @LegacyUserCode, @PersonId, @ContactName);
        """;

    internal const string PhoneUpdateSql = """
        UPDATE dbo.CTELEFONO
        SET TTL_FL_CVE = @PhoneTypeCode,
            DMO_FL_CVE = @AddressId,
            TFN_CL_LARGA_DISTANCIA = @LongDistanceCode,
            TFN_CL_LADA = @AreaCode,
            TFN_CL_TELEFONO = @PhoneNumber,
            TFN_CL_EXTENSION = @Extension,
            TFN_DS_RAZON_INACTIVO = @InactiveReason,
            TFN_FG_REGDEFAULT = @IsDefault,
            TFN_FE_ULTMOD = @OperationDate,
            USR_CL_CVE = @LegacyUserCode,
            TFN_DS_CONTACTO = @ContactName
        WHERE TFN_FL_CVE = @PhoneId AND PNA_FL_PERSONA = @PersonId AND TFN_FE_ULTMOD = @ExpectedModifiedAt;
        """;

    internal const string PhoneStateUpdateSql = """
        UPDATE dbo.CTELEFONO
        SET TFN_FG_STATUS = @StatusCode,
            TFN_FG_REGDEFAULT = @IsDefault,
            TFN_FE_ULTMOD = @OperationDate,
            USR_CL_CVE = @LegacyUserCode
        WHERE TFN_FL_CVE = @PhoneId AND PNA_FL_PERSONA = @PersonId AND TFN_FE_ULTMOD = @ExpectedModifiedAt;
        """;

    public Task<ManagedCustomerPhone> CreateAsync(CustomerPhoneCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("phone", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            var normalized = ValidateCreate(command);
            await EnsurePersonExistsAsync(connection, transaction, command.PersonId, cancellationToken);
            await EnsureTypeAndAddressAsync(connection, transaction, command.PersonId, normalized.PhoneTypeCode, normalized.AddressId, cancellationToken);
            var hasDefault = await HasActiveDefaultAsync(connection, transaction, command.PersonId, cancellationToken);
            var isDefault = !hasDefault || command.IsDefault;
            var phoneId = await NextIdAsync(connection, transaction, cancellationToken);
            if (isDefault)
                await ClearOtherDefaultsAsync(connection, transaction, command.PersonId, phoneId, operationDate, legacyUserCode, cancellationToken);

            var parameters = PhoneParameters(command.PersonId, phoneId, normalized, isDefault, operationDate, legacyUserCode);
            await connection.ExecuteAsync(new CommandDefinition(PhoneInsertSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            await WriteAuditAsync(connection, transaction, 4, operationDate, legacyUserCode, cancellationToken);
            return await LoadPhoneAsync(connection, transaction, command.PersonId, phoneId, cancellationToken);
        }, cancellationToken);

    public Task<ManagedCustomerPhone> UpdateAsync(CustomerPhoneUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("phone", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            var normalized = ValidateUpdate(command);
            await EnsurePersonExistsAsync(connection, transaction, command.PersonId, cancellationToken);
            await EnsureTypeAndAddressAsync(connection, transaction, command.PersonId, normalized.PhoneTypeCode, normalized.AddressId, cancellationToken);
            var current = await LoadPhoneAsync(connection, transaction, command.PersonId, command.PhoneId, cancellationToken);
            EnsureExpected(current, command.ExpectedModifiedAt);
            if (current.StatusCode == 0)
                throw new CustomerPhoneValidationException("Historical phones can only be activated.");
            if (current.IsDefault && !command.IsDefault)
                throw new CustomerPhoneConflictException("phone_default_required", "An active replacement phone is required.");
            if (command.IsDefault && current.StatusCode != 1)
                throw new CustomerPhoneConflictException("phone_default_required", "Only an active phone can be default.");
            var hasOtherDefault = await HasOtherActiveDefaultAsync(connection, transaction, command.PersonId, command.PhoneId, cancellationToken);
            var isDefault = current.StatusCode == 1 && (command.IsDefault || !hasOtherDefault);
            if (isDefault)
                await ClearOtherDefaultsAsync(connection, transaction, command.PersonId, command.PhoneId, operationDate, legacyUserCode, cancellationToken);

            var parameters = PhoneParameters(command.PersonId, command.PhoneId, normalized, isDefault, operationDate, legacyUserCode);
            parameters.Add("ExpectedModifiedAt", command.ExpectedModifiedAt, DbType.DateTime);
            parameters.Add("InactiveReason", current.InactiveReason, DbType.String, size: 255);
            var affected = await connection.ExecuteAsync(new CommandDefinition(PhoneUpdateSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            if (affected == 0) throw new CustomerPhoneConflictException("phone_modified", "The phone was modified by another operation.");
            await WriteAuditAsync(connection, transaction, 5, operationDate, legacyUserCode, cancellationToken);
            return await LoadPhoneAsync(connection, transaction, command.PersonId, command.PhoneId, cancellationToken);
        }, cancellationToken);

    public Task<ManagedCustomerPhone> ActivateAsync(CustomerPhoneStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ChangeStateAsync(command, true, legacyUserCode, correlationId, cancellationToken);

    public Task<ManagedCustomerPhone> DeactivateAsync(CustomerPhoneStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ChangeStateAsync(command, false, legacyUserCode, correlationId, cancellationToken);

    private Task<ManagedCustomerPhone> ChangeStateAsync(CustomerPhoneStateChangeCommand command, bool activate, string legacyUserCode, string correlationId, CancellationToken cancellationToken) =>
        ExecuteAsync("phone", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            await EnsurePersonExistsAsync(connection, transaction, command.PersonId, cancellationToken);
            var current = await LoadPhoneAsync(connection, transaction, command.PersonId, command.PhoneId, cancellationToken);
            EnsureExpected(current, command.ExpectedModifiedAt);
            if (!activate && current.StatusCode == 0)
                throw new CustomerPhoneValidationException("Historical phones can only be activated.");

            var hasActiveDefault = await HasActiveDefaultAsync(connection, transaction, command.PersonId, cancellationToken);
            var isDefault = current.IsDefault;
            if (activate && (!hasActiveDefault || current.IsDefault))
            {
                await ClearOtherDefaultsAsync(connection, transaction, command.PersonId, command.PhoneId, operationDate, legacyUserCode, cancellationToken);
                isDefault = true;
            }

            if (!activate && current.IsDefault)
            {
                if (command.ReplacementPhoneId is null || command.ReplacementPhoneId == command.PhoneId)
                    throw new CustomerPhoneConflictException("phone_default_required", "An active replacement phone is required.");
                var replacement = await LoadPhoneAsync(connection, transaction, command.PersonId, command.ReplacementPhoneId.Value, cancellationToken);
                if (replacement.StatusCode != 1)
                    throw new CustomerPhoneConflictException("phone_default_required", "An active replacement phone is required.");
                await ClearOtherDefaultsAsync(connection, transaction, command.PersonId, replacement.PhoneId, operationDate, legacyUserCode, cancellationToken);
                await SetDefaultAsync(connection, transaction, replacement.PhoneId, command.PersonId, operationDate, legacyUserCode, cancellationToken);
                isDefault = false;
            }

            var parameters = new DynamicParameters();
            parameters.Add("PhoneId", command.PhoneId, DbType.Int32);
            parameters.Add("PersonId", command.PersonId, DbType.Int32);
            parameters.Add("ExpectedModifiedAt", command.ExpectedModifiedAt, DbType.DateTime);
            parameters.Add("StatusCode", activate ? 1 : 2, DbType.Byte);
            parameters.Add("IsDefault", isDefault ? 1 : 0, DbType.Byte);
            parameters.Add("OperationDate", operationDate, DbType.DateTime);
            parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8);
            var affected = await connection.ExecuteAsync(new CommandDefinition(PhoneStateUpdateSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            if (affected == 0) throw new CustomerPhoneConflictException("phone_modified", "The phone was modified by another operation.");
            await WriteAuditAsync(connection, transaction, 5, operationDate, legacyUserCode, cancellationToken);
            return await LoadPhoneAsync(connection, transaction, command.PersonId, command.PhoneId, cancellationToken);
        }, cancellationToken);

    private async Task<T> ExecuteAsync<T>(string stage, int personId, string legacyUserCode, string correlationId, Func<SqlConnection, SqlTransaction, DateTime, Task<T>> operation, CancellationToken cancellationToken)
    {
        await ValidateTenantAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(legacyUserCode) || legacyUserCode.Trim().Length > 8)
            throw new CustomerPhoneValidationException("A valid Legacy user code is required.");
        EnsureWriteConfigured();
        await using var connection = new SqlConnection(_options.WriteConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await EnsureTestDatabaseAsync(connection, cancellationToken);
        }
        catch (LegacyWriteNotConfiguredException) { throw; }
        catch (SqlException exception) { throw new LegacyWriteUnavailableException("connection", exception); }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(connection, transaction, DateTime.UtcNow);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            LogFailure(_logger, exception.GetType().Name, stage, exception is SqlException sql ? sql.Number : null, correlationId);
            throw;
        }
    }

    private async Task ValidateTenantAsync(CancellationToken cancellationToken)
    {
        var tenant = await _tenantContext.GetAsync(cancellationToken);
        var deploymentTenant = configuration["Deployment:TenantCode"];
        if (tenant is null || string.IsNullOrWhiteSpace(deploymentTenant) || !string.Equals(tenant.TenantCode, deploymentTenant, StringComparison.Ordinal))
            throw new CustomerPhoneValidationException("The selected tenant is not valid for this deployment.");
    }

    private void EnsureWriteConfigured()
    {
        var reason = LegacyCustomerWriteRepository.GetConfigurationReason(_options, hostEnvironment.IsDevelopment());
        if (reason is not null)
            throw new LegacyWriteNotConfiguredException("Legacy write is not configured.", "configuration", reason.Value);
    }

    private static async Task EnsureTestDatabaseAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var database = await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT DB_NAME();", cancellationToken: cancellationToken));
        if (!string.Equals(database, "pr_t", StringComparison.Ordinal))
            throw new LegacyWriteNotConfiguredException("Legacy write database is not approved.", "database_guard", LegacyWriteConfigurationReason.InvalidExpectedDatabase);
    }

    private static async Task EnsurePersonExistsAsync(SqlConnection connection, SqlTransaction transaction, int personId, CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPERSONA WHERE PNA_FL_PERSONA = @PersonId) THEN 1 ELSE 0 END;", new { PersonId = personId }, transaction, cancellationToken: cancellationToken));
        if (exists == 0) throw new CustomerPhoneNotFoundException("Customer was not found.");
    }

    private static async Task EnsureTypeAndAddressAsync(SqlConnection connection, SqlTransaction transaction, int personId, int phoneTypeCode, int addressId, CancellationToken cancellationToken)
    {
        var typeExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CTTELEFONO WHERE TTL_FL_CVE = @PhoneTypeCode AND TTL_FG_STATUS = 1) THEN 1 ELSE 0 END;", new { PhoneTypeCode = phoneTypeCode }, transaction, cancellationToken: cancellationToken));
        if (typeExists == 0) throw new CustomerPhoneValidationException("The phone type is not active.");
        var addressExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CDOMICILIO WHERE DMO_FL_CVE = @AddressId AND PNA_FL_PERSONA = @PersonId AND DMO_FG_STATUS = 1) THEN 1 ELSE 0 END;", new { PersonId = personId, AddressId = addressId }, transaction, cancellationToken: cancellationToken));
        if (addressExists == 0) throw new CustomerPhoneValidationException("The selected address is not active for the customer.");
    }

    private static async Task<ManagedCustomerPhone> LoadPhoneAsync(SqlConnection connection, SqlTransaction transaction, int personId, int phoneId, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<LegacyCustomerPhoneRow>(new CommandDefinition(PhoneSelectSql, new { PersonId = personId, PhoneId = phoneId }, transaction, cancellationToken: cancellationToken));
        return row is null ? throw new CustomerPhoneNotFoundException("Phone was not found.") : Map(row);
    }

    internal static ManagedCustomerPhone Map(LegacyCustomerPhoneRow row) => new(
        row.PhoneId,
        row.PersonId,
        row.PhoneTypeCode,
        row.AddressId ?? throw new InvalidOperationException("Legacy phone address was unexpectedly null."),
        NormalizeLegacyString(row.LongDistanceCode),
        NormalizeLegacyString(row.AreaCode),
        NormalizeLegacyString(row.PhoneNumber),
        NormalizeLegacyString(row.Extension),
        row.StatusCode,
        NormalizeLegacyString(row.InactiveReason),
        row.IsDefault == 1,
        row.ModifiedAt ?? throw new InvalidOperationException("Legacy phone modified date was unexpectedly null."),
        NormalizeLegacyString(row.ContactName));

    private static string? NormalizeLegacyString(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<bool> HasActiveDefaultAsync(SqlConnection connection, SqlTransaction transaction, int personId, CancellationToken cancellationToken) =>
        await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CTELEFONO WITH (UPDLOCK, HOLDLOCK) WHERE PNA_FL_PERSONA = @PersonId AND TFN_FG_STATUS = 1 AND TFN_FG_REGDEFAULT = 1) THEN 1 ELSE 0 END;", new { PersonId = personId }, transaction, cancellationToken: cancellationToken)) == 1;

    private static async Task<bool> HasOtherActiveDefaultAsync(SqlConnection connection, SqlTransaction transaction, int personId, int phoneId, CancellationToken cancellationToken) =>
        await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CTELEFONO WITH (UPDLOCK, HOLDLOCK) WHERE PNA_FL_PERSONA = @PersonId AND TFN_FL_CVE <> @PhoneId AND TFN_FG_STATUS = 1 AND TFN_FG_REGDEFAULT = 1) THEN 1 ELSE 0 END;", new { PersonId = personId, PhoneId = phoneId }, transaction, cancellationToken: cancellationToken)) == 1;

    private static Task<int> ClearOtherDefaultsAsync(SqlConnection connection, SqlTransaction transaction, int personId, int phoneId, DateTime operationDate, string legacyUserCode, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("UPDATE dbo.CTELEFONO SET TFN_FG_REGDEFAULT = 0, TFN_FE_ULTMOD = @OperationDate, USR_CL_CVE = @LegacyUserCode WHERE PNA_FL_PERSONA = @PersonId AND TFN_FL_CVE <> @PhoneId AND TFN_FG_REGDEFAULT = 1;", new { PersonId = personId, PhoneId = phoneId, OperationDate = operationDate, LegacyUserCode = legacyUserCode.Trim() }, transaction, cancellationToken: cancellationToken));

    private static Task<int> SetDefaultAsync(SqlConnection connection, SqlTransaction transaction, int phoneId, int personId, DateTime operationDate, string legacyUserCode, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("UPDATE dbo.CTELEFONO SET TFN_FG_REGDEFAULT = 1, TFN_FE_ULTMOD = @OperationDate, USR_CL_CVE = @LegacyUserCode WHERE TFN_FL_CVE = @PhoneId AND PNA_FL_PERSONA = @PersonId AND TFN_FG_STATUS = 1;", new { PersonId = personId, PhoneId = phoneId, OperationDate = operationDate, LegacyUserCode = legacyUserCode.Trim() }, transaction, cancellationToken: cancellationToken));

    private static void EnsureExpected(ManagedCustomerPhone current, DateTime expectedModifiedAt)
    {
        if (current.ModifiedAt != expectedModifiedAt)
            throw new CustomerPhoneConflictException("phone_modified", "The phone was modified by another operation.");
    }

    private static DynamicParameters PhoneParameters(int personId, int phoneId, CustomerPhoneCreateCommand command, bool isDefault, DateTime operationDate, string legacyUserCode)
    {
        var parameters = new DynamicParameters();
        parameters.Add("PhoneId", phoneId, DbType.Int32);
        parameters.Add("PersonId", personId, DbType.Int32);
        parameters.Add("PhoneTypeCode", command.PhoneTypeCode, DbType.Byte);
        parameters.Add("AddressId", command.AddressId, DbType.Int32);
        parameters.Add("LongDistanceCode", command.LongDistanceCode, DbType.String, size: 10);
        parameters.Add("AreaCode", command.AreaCode, DbType.String, size: 10);
        parameters.Add("PhoneNumber", command.PhoneNumber, DbType.String, size: 20);
        parameters.Add("Extension", command.Extension, DbType.String, size: 10);
        parameters.Add("InactiveReason", null, DbType.String, size: 255);
        parameters.Add("IsDefault", isDefault ? 1 : 0, DbType.Byte);
        parameters.Add("OperationDate", operationDate, DbType.DateTime);
        parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8);
        parameters.Add("ContactName", command.ContactName, DbType.String, size: 150);
        return parameters;
    }

    private static CustomerPhoneCreateCommand ValidateCreate(CustomerPhoneCreateCommand command) => Normalize(command);

    private static CustomerPhoneCreateCommand ValidateUpdate(CustomerPhoneUpdateCommand command) => Normalize(new CustomerPhoneCreateCommand(command.PersonId, command.PhoneTypeCode, command.AddressId, command.LongDistanceCode, command.AreaCode, command.PhoneNumber, command.Extension, command.ContactName, command.IsDefault));

    private static CustomerPhoneCreateCommand Normalize(CustomerPhoneCreateCommand command)
    {
        CustomerPhoneRules.ValidateType(command.PhoneTypeCode);
        if (command.AddressId <= 0) throw new CustomerPhoneValidationException("A valid address is required.");
        return command with
        {
            LongDistanceCode = CustomerPhoneRules.NormalizeOptional(command.LongDistanceCode, 10, nameof(command.LongDistanceCode)),
            AreaCode = CustomerPhoneRules.NormalizeOptional(command.AreaCode, 10, nameof(command.AreaCode)),
            PhoneNumber = CustomerPhoneRules.NormalizeRequired(command.PhoneNumber, 20, nameof(command.PhoneNumber)),
            Extension = CustomerPhoneRules.NormalizeOptional(command.Extension, 10, nameof(command.Extension)),
            ContactName = CustomerPhoneRules.NormalizeOptional(command.ContactName, 150, nameof(command.ContactName)),
        };
    }

    private static async Task<int> NextIdAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken) =>
        await connection.ExecuteScalarAsync<int?>(new CommandDefinition("UPDATE dbo.CCATCONSEC WITH (UPDLOCK, HOLDLOCK) SET CCT_NO_CONSECUTIVO = CCT_NO_CONSECUTIVO + 1 OUTPUT INSERTED.CCT_NO_CONSECUTIVO WHERE EMP_FL_CVE = 0 AND CCS_DS_NOMTABLA = @TableName AND CCT_DS_CAMPO = @FieldName;", new { TableName = "CTELEFONO", FieldName = "TFN_FL_CVE" }, transaction, cancellationToken: cancellationToken))
        ?? throw new InvalidOperationException("No Legacy consecutive configured for CTELEFONO.");

    private static async Task WriteAuditAsync(SqlConnection connection, SqlTransaction transaction, int activityCode, DateTime operationDate, string legacyUserCode, CancellationToken cancellationToken)
    {
        var bitacoraId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("UPDATE dbo.CCATCONSEC WITH (UPDLOCK, HOLDLOCK) SET CCT_NO_CONSECUTIVO = CCT_NO_CONSECUTIVO + 1 OUTPUT INSERTED.CCT_NO_CONSECUTIVO WHERE EMP_FL_CVE = 0 AND CCS_DS_NOMTABLA = @TableName;", new { TableName = "KBITACORA" }, transaction, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException("No Legacy consecutive configured for KBITACORA.");
        await connection.ExecuteAsync(new CommandDefinition("INSERT INTO dbo.KBITACORA (BIT_FL_CVE, BIT_FE_FECHA, ATV_FL_CVE, BIT_FE_OPERACION, BIT_DS_REFERENCIA, USR_CL_CVE, USR_CL_FIRMA, BIT_TOP_CVE) VALUES (@BitacoraId, SYSUTCDATETIME(), @ActivityCode, @OperationDate, @Reference, @LegacyUserCode, @LegacyUserCode, '');", new { BitacoraId = bitacoraId, ActivityCode = activityCode, OperationDate = operationDate, Reference = "Customer phone operation", LegacyUserCode = legacyUserCode.Trim() }, transaction, cancellationToken: cancellationToken));
    }

    [LoggerMessage(EventId = 4210, Level = LogLevel.Error, Message = "Customer phone mutation failed. ExceptionType={ExceptionType} SqlNumber={SqlNumber} Stage={Stage} CorrelationId={CorrelationId}")]
    private static partial void LogFailure(ILogger logger, string exceptionType, string stage, int? sqlNumber, string correlationId);
}
