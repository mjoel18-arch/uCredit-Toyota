using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

internal sealed class LegacyManagedCustomerEmailRow
{
    public int EmailId { get; init; }
    public int PersonId { get; init; }
    public string? Contact { get; init; }
    public string? Email { get; init; }
    public int StatusCode { get; init; }
    public DateTime ModifiedAt { get; init; }
}

internal sealed class LegacyCustomerEmailUsageRow
{
    public int EmailId { get; init; }
    public int Code { get; init; }
}

internal sealed class LegacyCustomerEmailUsageCatalogRow
{
    public int Code { get; init; }
    public string Description { get; init; } = string.Empty;
}

public sealed class LegacyCustomerEmailReadRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext tenantContext) : ICustomerEmailReadRepository, ICustomerEmailUsageRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;

    internal const string ReadSql = """
        SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPERSONA WHERE PNA_FL_PERSONA = @PersonId) THEN 1 ELSE 0 END AS CustomerExists;
        SELECT MAI_FL_CVE AS EmailId, PNA_FL_PERSONA AS PersonId,
               MAI_DS_CONTACTO AS Contact, MAI_DS_EMAIL AS Email,
               MAI_FG_STATUS AS StatusCode, MAI_FE_ULTMOD AS ModifiedAt
        FROM dbo.CPERSONA_EMAIL
        WHERE PNA_FL_PERSONA = @PersonId
        ORDER BY MAI_FG_STATUS DESC, MAI_FL_CVE ASC;
        SELECT MAI_FL_CVE AS EmailId, PAR_CL_VALOR AS Code
        FROM dbo.KEMAIL_USO
        WHERE MAI_FL_CVE IN (SELECT MAI_FL_CVE FROM dbo.CPERSONA_EMAIL WHERE PNA_FL_PERSONA = @PersonId);
        """;

    internal const string UsageCatalogSql = """
        SELECT PAR_CL_VALOR AS Code, PAR_DS_DESCRIPCION AS Description
        FROM dbo.CPARAMETRO
        WHERE PAR_FL_CVE = 244 AND PAR_CL_VALOR > 0 AND PAR_FG_STATUS = 1
        ORDER BY PAR_DS_DESCRIPCION ASC, PAR_CL_VALOR ASC;
        """;

    public async Task<IReadOnlyList<ManagedCustomerEmail>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        if (await _tenantContext.GetAsync(cancellationToken) is null)
            return null;

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        await connection.OpenAsync(cancellationToken);
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            ReadSql, new { PersonId = personId }, commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text, cancellationToken: cancellationToken));

        if (await result.ReadFirstAsync<int>() == 0)
            return null;

        var emails = (await result.ReadAsync<LegacyManagedCustomerEmailRow>()).ToArray();
        var usages = (await result.ReadAsync<LegacyCustomerEmailUsageRow>()).ToLookup(row => row.EmailId, row => row.Code);
        return emails.Select(row => Map(row, usages[row.EmailId])).ToArray();
    }

    public async Task<IReadOnlyList<CustomerEmailUsage>> GetActiveUsagesAsync(CancellationToken cancellationToken = default)
    {
        if (await _tenantContext.GetAsync(cancellationToken) is null)
            return [];

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<LegacyCustomerEmailUsageCatalogRow>(new CommandDefinition(
            UsageCatalogSql, commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text, cancellationToken: cancellationToken));
        return rows.Select(row => new CustomerEmailUsage(row.Code, row.Description)).ToArray();
    }

    private static ManagedCustomerEmail Map(LegacyManagedCustomerEmailRow row, IEnumerable<int> usageCodes) =>
        new(row.EmailId, row.PersonId, row.Contact, row.Email ?? throw new InvalidOperationException("Legacy email was unexpectedly null."),
            row.StatusCode, usageCodes.OrderBy(code => code).ToArray(), row.ModifiedAt);
}
