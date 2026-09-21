using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

internal sealed class ManagedAddressDbRow
{
    public int? AddressId { get; set; }
    public int? PersonId { get; set; }
    public string? PostalCode { get; set; }
    public string? State { get; set; }
    public string? Municipality { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? StreetAndNumber { get; set; }
    public string? ExteriorNumber { get; set; }
    public string? InteriorNumber { get; set; }
    public int? AddressTypeCode { get; set; }
    public string? AddressTypeDescription { get; set; }
    public int? IsActive { get; set; }
    public int? IsDefault { get; set; }
    public int? Billing { get; set; }
    public int? Statements { get; set; }
    public int? Other { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public int? CountryCode { get; set; }
}

public sealed partial class LegacyCustomerWriteRepository
{
    internal const string AddressSelectSql = """
        SELECT D.DMO_FL_CVE AS AddressId,
               D.PNA_FL_PERSONA AS PersonId,
               D.DMO_CL_CPOSTAL AS PostalCode,
               D.DMO_DS_EFEDERATIVA AS State,
               D.DMO_DS_MUNICIPIO AS Municipality,
               D.DMO_DS_CIUDAD AS City,
               D.DMO_DS_COLONIA AS Neighborhood,
               D.DMO_DS_CALLE_NUM AS StreetAndNumber,
               D.DMO_DS_NUMEXT AS ExteriorNumber,
               D.DMO_DS_NUMINT AS InteriorNumber,
               D.DMO_FG_TDIRECCION AS AddressTypeCode,
               T.PAR_DS_DESCRIPCION AS AddressTypeDescription,
               D.DMO_FG_STATUS AS IsActive,
               D.DMO_FG_REGDEFAULT AS IsDefault,
               D.DMO_FG_FACTURA AS Billing,
               D.DMO_FG_EDOCTA AS Statements,
               D.DMO_FG_OTROS AS Other,
               D.DMO_FE_ULTMOD AS ModifiedAt,
               D.PAI_FL_CVE AS CountryCode
        FROM dbo.CDOMICILIO AS D
        OUTER APPLY
        (
            SELECT TOP (1) P.PAR_DS_DESCRIPCION
            FROM dbo.CPARAMETRO AS P
            WHERE P.PAR_FL_CVE = 7
              AND P.PAR_CL_VALOR = D.DMO_FG_TDIRECCION
        ) AS T
        WHERE D.DMO_FL_CVE = @AddressId
          AND D.PNA_FL_PERSONA = @PersonId;
        """;

    internal const string AddressExistsSql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPERSONA WHERE PNA_FL_PERSONA = @PersonId) THEN 1 ELSE 0 END;";
    internal const string AddressDefaultClearSql = "UPDATE dbo.CDOMICILIO SET DMO_FG_REGDEFAULT = 0, DMO_FE_ULTMOD = @OperationDate, USR_CL_CVE = @LegacyUserCode WHERE PNA_FL_PERSONA = @PersonId AND DMO_FL_CVE <> @AddressId AND DMO_FG_REGDEFAULT = 1;";
    internal const string AddressInsertSql = """
        INSERT INTO dbo.CDOMICILIO
        (DMO_FL_CVE, PNA_FL_PERSONA, DMO_CL_CPOSTAL, DMO_DS_EFEDERATIVA, DMO_DS_MUNICIPIO, DMO_DS_CIUDAD, DMO_DS_COLONIA, DMO_DS_CALLE_NUM, DMO_DS_NUMEXT, DMO_DS_NUMINT, DMO_DS_REFERENCIA, DMO_DS_HORARIO, DMO_FG_TDIRECCION, DMO_FG_STATUS, DMO_FG_REGDEFAULT, DMO_FG_FACTURA, DMO_FG_EDOCTA, DMO_FG_OTROS, DMO_FE_ULTMOD, USR_CL_CVE, PAI_FL_CVE)
        VALUES
        (@AddressId, @PersonId, @PostalCode, @State, @Municipality, @City, @Neighborhood, @StreetAndNumber, @ExteriorNumber, @InteriorNumber, @Reference, @Schedule, @AddressTypeCode, 1, @IsDefault, @Billing, @Statements, @Other, @OperationDate, @LegacyUserCode, @CountryCode);
        """;
    internal const string AddressUpdateSql = """
        UPDATE dbo.CDOMICILIO
        SET DMO_CL_CPOSTAL = @PostalCode,
            DMO_DS_EFEDERATIVA = @State,
            DMO_DS_MUNICIPIO = @Municipality,
            DMO_DS_CIUDAD = @City,
            DMO_DS_COLONIA = @Neighborhood,
            DMO_DS_CALLE_NUM = @StreetAndNumber,
            DMO_DS_NUMEXT = @ExteriorNumber,
            DMO_DS_NUMINT = @InteriorNumber,
            DMO_DS_REFERENCIA = @Reference,
            DMO_DS_HORARIO = @Schedule,
            DMO_FG_TDIRECCION = @AddressTypeCode,
            DMO_FG_REGDEFAULT = @IsDefault,
            DMO_FG_FACTURA = @Billing,
            DMO_FG_EDOCTA = @Statements,
            DMO_FG_OTROS = @Other,
            DMO_FE_ULTMOD = @OperationDate,
            USR_CL_CVE = @LegacyUserCode,
            PAI_FL_CVE = @CountryCode
        WHERE DMO_FL_CVE = @AddressId
          AND PNA_FL_PERSONA = @PersonId
          AND DMO_FE_ULTMOD = @ExpectedModifiedAt;
        """;
    internal const string AddressStateUpdateSql = """
        UPDATE dbo.CDOMICILIO
        SET DMO_FG_STATUS = @IsActive,
            DMO_FG_REGDEFAULT = @IsDefault,
            DMO_FE_ULTMOD = @OperationDate,
            USR_CL_CVE = @LegacyUserCode
        WHERE DMO_FL_CVE = @AddressId
          AND PNA_FL_PERSONA = @PersonId
          AND DMO_FE_ULTMOD = @ExpectedModifiedAt;
        """;

    public Task<ManagedCustomerAddress> CreateAsync(CustomerAddressCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ExecuteAddressMutationAsync("address", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            ValidateInput(command);
            var flags = CustomerAddressRules.ToLegacyFlags(command.AddressTypeCode, command.Uses);
            await EnsurePersonExistsAsync(connection, transaction, command.PersonId, cancellationToken);
            var hasAnyAddress = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CDOMICILIO WHERE PNA_FL_PERSONA = @PersonId) THEN 1 ELSE 0 END;", new { command.PersonId }, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken)) == 1;
            var hasDefault = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CDOMICILIO WHERE PNA_FL_PERSONA = @PersonId AND DMO_FG_STATUS = 1 AND DMO_FG_REGDEFAULT = 1) THEN 1 ELSE 0 END;", new { command.PersonId }, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken)) == 1;
            var isDefault = !hasAnyAddress || command.IsDefault || !hasDefault;
            var addressId = await NextIdAsync(connection, transaction, "CDOMICILIO", cancellationToken);
            if (isDefault)
                await ClearOtherDefaultsAsync(connection, transaction, command.PersonId, addressId, operationDate, legacyUserCode, cancellationToken);

            var parameters = AddressParameters(command.PersonId, addressId, command, flags, operationDate, legacyUserCode);
            await connection.ExecuteAsync(new CommandDefinition(AddressInsertSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            await WriteAddressAuditAsync(connection, transaction, command.PersonId, operationDate, legacyUserCode, 4, cancellationToken);
            return await LoadAddressAsync(connection, transaction, command.PersonId, addressId, cancellationToken);
        }, cancellationToken);

    public Task<ManagedCustomerAddress> UpdateAsync(CustomerAddressUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ExecuteAddressMutationAsync("address", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            ValidateInput(command);
            var flags = CustomerAddressRules.ToLegacyFlags(command.AddressTypeCode, command.Uses);
            await EnsurePersonExistsAsync(connection, transaction, command.PersonId, cancellationToken);
            var current = await LoadAddressForUpdateAsync(connection, transaction, command.PersonId, command.AddressId, cancellationToken);
            EnsureExpected(current, command.ExpectedModifiedAt);
            if (current.IsDefault == 1 && !command.IsDefault)
                throw new CustomerAddressConflictException("address_default_required", "The current default address requires a replacement.");
            if (command.IsDefault && current.IsActive != 1)
                throw new CustomerAddressConflictException("address_default_required", "An inactive address cannot be default.");

            var hasOtherDefault = await HasOtherActiveDefaultAsync(connection, transaction, command.PersonId, command.AddressId, cancellationToken);
            var isDefault = command.IsDefault || !hasOtherDefault;
            if (isDefault)
                await ClearOtherDefaultsAsync(connection, transaction, command.PersonId, command.AddressId, operationDate, legacyUserCode, cancellationToken);

            var parameters = AddressParameters(command.PersonId, command.AddressId, command, flags, operationDate, legacyUserCode);
            parameters.Add("ExpectedModifiedAt", command.ExpectedModifiedAt, DbType.DateTime);
            parameters.Add("IsDefault", isDefault ? 1 : 0, DbType.Int32);
            var affected = await connection.ExecuteAsync(new CommandDefinition(AddressUpdateSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            if (affected == 0) throw new CustomerAddressConflictException("address_modified", "The address was modified by another operation.");
            await WriteAddressAuditAsync(connection, transaction, command.PersonId, operationDate, legacyUserCode, 5, cancellationToken);
            return await LoadAddressAsync(connection, transaction, command.PersonId, command.AddressId, cancellationToken);
        }, cancellationToken);

    public Task<ManagedCustomerAddress> ActivateAsync(CustomerAddressStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ChangeAddressStateAsync(command, true, legacyUserCode, correlationId, cancellationToken);

    public Task<ManagedCustomerAddress> DeactivateAsync(CustomerAddressStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ChangeAddressStateAsync(command, false, legacyUserCode, correlationId, cancellationToken);

    private Task<ManagedCustomerAddress> ChangeAddressStateAsync(CustomerAddressStateChangeCommand command, bool activate, string legacyUserCode, string correlationId, CancellationToken cancellationToken) =>
        ExecuteAddressMutationAsync("address", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            await EnsurePersonExistsAsync(connection, transaction, command.PersonId, cancellationToken);
            var current = await LoadAddressForUpdateAsync(connection, transaction, command.PersonId, command.AddressId, cancellationToken);
            EnsureExpected(current, command.ExpectedModifiedAt);
            var isDefault = current.IsDefault == 1;

            if (!activate && current.IsDefault == 1)
            {
                if (command.ReplacementAddressId is null || command.ReplacementAddressId == command.AddressId)
                    throw new CustomerAddressConflictException("address_default_required", "A replacement active address is required.");
                var replacement = await LoadAddressForUpdateAsync(connection, transaction, command.PersonId, command.ReplacementAddressId.Value, cancellationToken);
                if (replacement.IsActive != 1)
                    throw new CustomerAddressConflictException("address_default_required", "A replacement active address is required.");
                await SetDefaultAsync(connection, transaction, command.PersonId, Require(replacement.AddressId, nameof(replacement.AddressId)), operationDate, legacyUserCode, cancellationToken);
                isDefault = false;
            }
            else if (activate && !await HasOtherActiveDefaultAsync(connection, transaction, command.PersonId, command.AddressId, cancellationToken))
            {
                await ClearOtherDefaultsAsync(connection, transaction, command.PersonId, command.AddressId, operationDate, legacyUserCode, cancellationToken);
                isDefault = true;
            }

            var parameters = new DynamicParameters();
            parameters.Add("PersonId", command.PersonId, DbType.Int32);
            parameters.Add("AddressId", command.AddressId, DbType.Int32);
            parameters.Add("ExpectedModifiedAt", command.ExpectedModifiedAt, DbType.DateTime);
            parameters.Add("IsActive", activate ? 1 : 0, DbType.Int32);
            parameters.Add("IsDefault", isDefault ? 1 : 0, DbType.Int32);
            parameters.Add("OperationDate", operationDate, DbType.DateTime);
            parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8);
            var affected = await connection.ExecuteAsync(new CommandDefinition(AddressStateUpdateSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            if (affected == 0) throw new CustomerAddressConflictException("address_modified", "The address was modified by another operation.");
            await WriteAddressAuditAsync(connection, transaction, command.PersonId, operationDate, legacyUserCode, 5, cancellationToken);
            return await LoadAddressAsync(connection, transaction, command.PersonId, command.AddressId, cancellationToken);
        }, cancellationToken);

    private async Task<T> ExecuteAddressMutationAsync<T>(string stage, int personId, string legacyUserCode, string correlationId, Func<SqlConnection, SqlTransaction, DateTime, Task<T>> operation, CancellationToken cancellationToken)
    {
        await ValidateTenantAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(legacyUserCode) || legacyUserCode.Trim().Length > 8)
            throw new CustomerAddressValidationException("A valid Legacy user code is required.");
        EnsureWriteConfigured();
        await using var connection = new SqlConnection(_options.WriteConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await EnsureTestDatabaseAsync(connection, cancellationToken);
        }
        catch (LegacyWriteNotConfiguredException)
        {
            throw;
        }
        catch (SqlException exception)
        {
            throw new LegacyWriteUnavailableException("connection", exception);
        }

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
            LogAddressFailure(_logger, exception.GetType().Name, stage, exception is SqlException sqlException ? sqlException.Number : null, correlationId);
            throw;
        }
    }

    private async Task ValidateTenantAsync(CancellationToken cancellationToken)
    {
        var tenant = await _tenantContext.GetAsync(cancellationToken);
        var deploymentTenant = _configuration["Deployment:TenantCode"];
        if (tenant is null || string.IsNullOrWhiteSpace(deploymentTenant) || !string.Equals(tenant.TenantCode, deploymentTenant, StringComparison.Ordinal))
            throw new CustomerAddressValidationException("The selected tenant is not valid for this deployment.");
    }

    private static void ValidateInput(CustomerAddressCreateCommand command)
    {
        ValidateText(command.PostalCode, 10, nameof(command.PostalCode));
        ValidateText(command.State, 70, nameof(command.State));
        ValidateText(command.Municipality, 70, nameof(command.Municipality));
        ValidateText(command.City, 70, nameof(command.City));
        ValidateText(command.Neighborhood, 70, nameof(command.Neighborhood));
        ValidateText(command.StreetAndNumber, 200, nameof(command.StreetAndNumber));
        ValidateText(command.ExteriorNumber, 100, nameof(command.ExteriorNumber));
        ValidateOptional(command.InteriorNumber, 100, nameof(command.InteriorNumber));
        ValidateOptional(command.Reference, 100, nameof(command.Reference));
        ValidateOptional(command.Schedule, 100, nameof(command.Schedule));
        if (command.CountryCode <= 0) throw new CustomerAddressValidationException("Country code is required.");
        CustomerAddressRules.NormalizeUses(command.Uses, command.AddressTypeCode);
    }

    private static void ValidateInput(CustomerAddressUpdateCommand command) =>
        ValidateInput(new CustomerAddressCreateCommand(command.PersonId, command.PostalCode, command.State, command.Municipality, command.City, command.Neighborhood, command.StreetAndNumber, command.ExteriorNumber, command.InteriorNumber, command.Reference, command.Schedule, command.AddressTypeCode, command.Uses, command.IsDefault, command.CountryCode));

    private static void ValidateText(string value, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximum)
            throw new CustomerAddressValidationException($"{name} is required and must not exceed {maximum} characters.");
    }

    private static void ValidateOptional(string? value, int maximum, string name)
    {
        if (value is not null && value.Trim().Length > maximum)
            throw new CustomerAddressValidationException($"{name} must not exceed {maximum} characters.");
    }

    private static DynamicParameters AddressParameters(int personId, int addressId, CustomerAddressCreateCommand command, (int Billing, int Statements, int Other) flags, DateTime operationDate, string legacyUserCode)
    {
        var parameters = new DynamicParameters();
        parameters.Add("PersonId", personId, DbType.Int32);
        parameters.Add("AddressId", addressId, DbType.Int32);
        parameters.Add("PostalCode", command.PostalCode.Trim(), DbType.String, size: 10);
        parameters.Add("State", command.State.Trim(), DbType.String, size: 70);
        parameters.Add("Municipality", command.Municipality.Trim(), DbType.String, size: 70);
        parameters.Add("City", command.City.Trim(), DbType.String, size: 70);
        parameters.Add("Neighborhood", command.Neighborhood.Trim(), DbType.String, size: 70);
        parameters.Add("StreetAndNumber", command.StreetAndNumber.Trim(), DbType.String, size: 200);
        parameters.Add("ExteriorNumber", command.ExteriorNumber.Trim(), DbType.String, size: 100);
        parameters.Add("InteriorNumber", LegacyCustomerWriteRepository.NormalizeOptionalLegacyString(command.InteriorNumber, 100), DbType.String, size: 100);
        parameters.Add("Reference", command.Reference?.Trim(), DbType.String, size: 100);
        parameters.Add("Schedule", command.Schedule?.Trim(), DbType.String, size: 100);
        parameters.Add("AddressTypeCode", command.AddressTypeCode, DbType.Int32);
        parameters.Add("Billing", flags.Billing, DbType.Int32);
        parameters.Add("Statements", flags.Statements, DbType.Int32);
        parameters.Add("Other", flags.Other, DbType.Int32);
        parameters.Add("OperationDate", operationDate, DbType.DateTime);
        parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8);
        parameters.Add("CountryCode", command.CountryCode, DbType.Int32);
        parameters.Add("IsDefault", command.IsDefault ? 1 : 0, DbType.Int32);
        return parameters;
    }

    private static DynamicParameters AddressParameters(int personId, int addressId, CustomerAddressUpdateCommand command, (int Billing, int Statements, int Other) flags, DateTime operationDate, string legacyUserCode)
    {
        var create = new CustomerAddressCreateCommand(personId, command.PostalCode, command.State, command.Municipality, command.City, command.Neighborhood, command.StreetAndNumber, command.ExteriorNumber, command.InteriorNumber, command.Reference, command.Schedule, command.AddressTypeCode, command.Uses, command.IsDefault, command.CountryCode);
        return AddressParameters(personId, addressId, create, flags, operationDate, legacyUserCode);
    }

    private static async Task EnsurePersonExistsAsync(SqlConnection connection, SqlTransaction transaction, int personId, CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(AddressExistsSql, new { PersonId = personId }, transaction, cancellationToken: cancellationToken));
        if (exists == 0) throw new CustomerAddressNotFoundException("Customer was not found.");
    }

    private static async Task<ManagedAddressDbRow> LoadAddressForUpdateAsync(SqlConnection connection, SqlTransaction transaction, int personId, int addressId, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<ManagedAddressDbRow>(new CommandDefinition(AddressSelectSql + "", new { PersonId = personId, AddressId = addressId }, transaction, cancellationToken: cancellationToken));
        return row ?? throw new CustomerAddressNotFoundException("Address was not found.");
    }

    private static async Task<ManagedCustomerAddress> LoadAddressAsync(SqlConnection connection, SqlTransaction transaction, int personId, int addressId, CancellationToken cancellationToken)
    {
        var row = await LoadAddressForUpdateAsync(connection, transaction, personId, addressId, cancellationToken);
        return Map(row);
    }

    private static async Task<bool> HasOtherActiveDefaultAsync(SqlConnection connection, SqlTransaction transaction, int personId, int addressId, CancellationToken cancellationToken) =>
        await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CDOMICILIO WITH (UPDLOCK, HOLDLOCK) WHERE PNA_FL_PERSONA = @PersonId AND DMO_FL_CVE <> @AddressId AND DMO_FG_STATUS = 1 AND DMO_FG_REGDEFAULT = 1) THEN 1 ELSE 0 END;", new { PersonId = personId, AddressId = addressId }, transaction, cancellationToken: cancellationToken)) == 1;

    private static async Task ClearOtherDefaultsAsync(SqlConnection connection, SqlTransaction transaction, int personId, int addressId, DateTime operationDate, string legacyUserCode, CancellationToken cancellationToken) =>
        await connection.ExecuteAsync(new CommandDefinition(AddressDefaultClearSql, new { PersonId = personId, AddressId = addressId, OperationDate = operationDate, LegacyUserCode = legacyUserCode.Trim() }, transaction, cancellationToken: cancellationToken));

    private static async Task SetDefaultAsync(SqlConnection connection, SqlTransaction transaction, int personId, int addressId, DateTime operationDate, string legacyUserCode, CancellationToken cancellationToken)
    {
        await ClearOtherDefaultsAsync(connection, transaction, personId, addressId, operationDate, legacyUserCode, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("UPDATE dbo.CDOMICILIO SET DMO_FG_REGDEFAULT = 1, DMO_FE_ULTMOD = @OperationDate, USR_CL_CVE = @LegacyUserCode WHERE DMO_FL_CVE = @AddressId AND PNA_FL_PERSONA = @PersonId AND DMO_FG_STATUS = 1;", new { PersonId = personId, AddressId = addressId, OperationDate = operationDate, LegacyUserCode = legacyUserCode.Trim() }, transaction, cancellationToken: cancellationToken));
    }

    private static void EnsureExpected(ManagedAddressDbRow current, DateTime expectedModifiedAt)
    {
        if (current.ModifiedAt is null || current.ModifiedAt.Value != expectedModifiedAt)
            throw new CustomerAddressConflictException("address_modified", "The address was modified by another operation.");
    }

    private static ManagedCustomerAddress Map(ManagedAddressDbRow row) => new(
        Require(row.AddressId, nameof(row.AddressId)),
        Require(row.PersonId, nameof(row.PersonId)),
        row.PostalCode,
        row.State,
        row.Municipality,
        row.City,
        row.Neighborhood,
        row.StreetAndNumber,
        row.ExteriorNumber,
        row.InteriorNumber,
        Require(row.AddressTypeCode, nameof(row.AddressTypeCode)),
        row.AddressTypeDescription,
        new[]
        {
            row.Billing == 1 ? CustomerAddressUse.Billing : null,
            row.Statements == 1 ? CustomerAddressUse.Statements : null,
            row.Other == 1 ? CustomerAddressUse.Other : null,
        }.OfType<string>().ToArray(),
        Require(row.IsActive, nameof(row.IsActive)) == 1,
        Require(row.IsDefault, nameof(row.IsDefault)) == 1,
        row.ModifiedAt ?? throw new InvalidOperationException("Legacy address modified date was unexpectedly null."),
        Require(row.CountryCode, nameof(row.CountryCode)));

    private static int Require(int? value, string name) => value ?? throw new InvalidOperationException($"Legacy address column {name} was unexpectedly null.");

    private static async Task WriteAddressAuditAsync(SqlConnection connection, SqlTransaction transaction, int personId, DateTime operationDate, string legacyUserCode, int activityCode, CancellationToken cancellationToken)
    {
        var bitacoraId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("UPDATE dbo.CCATCONSEC WITH (UPDLOCK, HOLDLOCK) SET CCT_NO_CONSECUTIVO = CCT_NO_CONSECUTIVO + 1 OUTPUT INSERTED.CCT_NO_CONSECUTIVO WHERE EMP_FL_CVE = 0 AND CCS_DS_NOMTABLA = @TableName;", new { TableName = "KBITACORA" }, transaction, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException("No Legacy consecutive configured for KBITACORA.");
        await connection.ExecuteAsync(new CommandDefinition("INSERT INTO dbo.KBITACORA (BIT_FL_CVE, BIT_FE_FECHA, ATV_FL_CVE, BIT_FE_OPERACION, BIT_DS_REFERENCIA, USR_CL_CVE, USR_CL_FIRMA, BIT_TOP_CVE) VALUES (@BitacoraId, SYSUTCDATETIME(), @ActivityCode, @OperationDate, @Reference, @LegacyUserCode, @LegacyUserCode, '');", new { BitacoraId = bitacoraId, ActivityCode = activityCode, OperationDate = operationDate, Reference = "Customer address operation", LegacyUserCode = legacyUserCode.Trim() }, transaction, cancellationToken: cancellationToken));
    }

    [LoggerMessage(EventId = 4204, Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Customer address mutation failed. ExceptionType={ExceptionType} SqlNumber={SqlNumber} Stage={Stage} CorrelationId={CorrelationId}")]
    private static partial void LogAddressFailure(Microsoft.Extensions.Logging.ILogger logger, string exceptionType, string stage, int? sqlNumber, string correlationId);
}

public sealed class CustomerAddressNotFoundException(string message) : Exception(message);
