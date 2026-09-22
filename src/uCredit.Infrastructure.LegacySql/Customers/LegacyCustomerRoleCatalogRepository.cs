using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

internal sealed class LegacyCustomerRoleCatalogRow
{
    public int RoleCode { get; init; }
    public string? RoleName { get; init; }
}

public sealed class LegacyCustomerRoleCatalogRepository(IOptions<LegacySqlOptions> options) : ICustomerRoleCatalogRepository
{
    private readonly LegacySqlOptions _options = options.Value;

    internal const string ReadSql = """
        SELECT PAR_CL_VALOR AS RoleCode, PAR_DS_DESCRIPCION AS RoleName
        FROM dbo.CPARAMETRO
        WHERE PAR_FL_CVE = @CatalogCode AND PAR_FG_STATUS = 1 AND PAR_CL_VALOR > 0
        ORDER BY PAR_DS_DESCRIPCION ASC, PAR_CL_VALOR ASC;
        """;

    public async Task<IReadOnlyList<CustomerRoleOption>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_options.ReadConnectionString);
        await connection.OpenAsync(cancellationToken);
        var rows = (await connection.QueryAsync<LegacyCustomerRoleCatalogRow>(new CommandDefinition(
            ReadSql, new { CatalogCode = 5 }, commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text, cancellationToken: cancellationToken))).ToArray();

        var duplicate = rows.GroupBy(row => row.RoleCode).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new CustomerRoleCatalogConflictException("The person role catalog contains duplicate active role codes.");
        return rows.Select(row => new CustomerRoleOption(row.RoleCode, row.RoleName?.Trim() ?? throw new InvalidOperationException("An active person role has no description."))).ToArray();
    }
}
