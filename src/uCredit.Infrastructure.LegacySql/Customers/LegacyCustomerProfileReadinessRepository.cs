using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

public sealed class LegacyCustomerProfileReadinessRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext executionTenantContext) : ICustomerProfileReadinessRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _executionTenantContext = executionTenantContext;

    internal static string CreateSql() => """
        SELECT
            P.PNA_FL_PERSONA AS PersonId,
            CAST(1 AS bit) AS HasGeneralData,
            CAST(CASE WHEN EXISTS
                (SELECT 1 FROM dbo.CDOMICILIO AS D
                 WHERE D.PNA_FL_PERSONA = P.PNA_FL_PERSONA AND D.DMO_FG_STATUS = 1)
                THEN 1 ELSE 0 END AS bit) AS HasAddress,
            CAST(CASE WHEN EXISTS
                (SELECT 1 FROM dbo.CTELEFONO AS T
                 WHERE T.PNA_FL_PERSONA = P.PNA_FL_PERSONA AND T.TFN_FG_STATUS = 1)
                THEN 1 ELSE 0 END AS bit) AS HasPhone,
            CAST(CASE WHEN EXISTS
                (SELECT 1 FROM dbo.CPCUENTA AS A
                 WHERE A.PNA_FL_PERSONA = P.PNA_FL_PERSONA AND A.PCT_FG_STATUS = 1)
                THEN 1 ELSE 0 END AS bit) AS HasAccount
        FROM dbo.CPERSONA AS P
        WHERE P.PNA_FL_PERSONA = @PersonId
          AND P.PNA_FG_STATUS = 1;
        """;

    public async Task<CustomerProfileReadiness?> GetAsync(
        int personId,
        CancellationToken cancellationToken = default)
    {
        if (personId <= 0) throw new ArgumentException("personId must be greater than zero.", nameof(personId));
        if (await _executionTenantContext.GetAsync(cancellationToken) is null)
            throw new InvalidOperationException("No execution tenant is selected.");
        if (string.IsNullOrWhiteSpace(_options.ReadConnectionString))
            throw new InvalidOperationException("Legacy read connection is not configured.");

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        var parameters = new DynamicParameters();
        parameters.Add("PersonId", personId, DbType.Int32);
        var row = await connection.QuerySingleOrDefaultAsync<ReadinessRow>(new CommandDefinition(
            CreateSql(),
            parameters,
            commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new CustomerProfileReadiness(row.PersonId, row.HasGeneralData, row.HasAddress, row.HasPhone, row.HasAccount);
    }

    private sealed class ReadinessRow
    {
        public int PersonId { get; set; }
        public bool HasGeneralData { get; set; }
        public bool HasAddress { get; set; }
        public bool HasPhone { get; set; }
        public bool HasAccount { get; set; }
    }
}
