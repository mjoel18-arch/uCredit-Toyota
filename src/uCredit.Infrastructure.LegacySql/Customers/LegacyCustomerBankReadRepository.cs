using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

internal sealed class LegacyCustomerBankRow
{
    public short BankId { get; init; }
    public string? BankName { get; init; }
}

public sealed class LegacyCustomerBankReadRepository(IOptions<LegacySqlOptions> options, IExecutionTenantContext tenantContext) : ICustomerBankReadRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;

    internal const string ReadSql = """
        SELECT BCO_FL_CVE AS BankId, BCO_DS_NOMBRE AS BankName
        FROM dbo.CBANCO
        WHERE BCO_FG_STATUS = 1
          AND BCO_FG_REAL = 1
          AND BCO_DS_NOMBRE IS NOT NULL
          AND LTRIM(RTRIM(BCO_DS_NOMBRE)) <> ''
        ORDER BY BCO_DS_NOMBRE ASC, BCO_FL_CVE ASC;
        """;

    public async Task<IReadOnlyList<CustomerBank>> GetActiveRealAsync(CancellationToken cancellationToken = default)
    {
        if (await _tenantContext.GetAsync(cancellationToken) is null) return [];
        await using var connection = new SqlConnection(_options.ReadConnectionString);
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<LegacyCustomerBankRow>(new CommandDefinition(
            ReadSql,
            commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
        return rows.Select(row => new CustomerBank(row.BankId, row.BankName!.Trim())).ToArray();
    }
}
