using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

internal sealed class LegacyCustomerAccountReadRow
{
    public int AccountId { get; init; }
    public int PersonId { get; init; }
    public short BankId { get; init; }
    public string? BankName { get; init; }
    public short BranchNumber { get; init; }
    public byte CurrencyCode { get; init; }
    public string? CurrencyName { get; init; }
    public byte AccountTypeCode { get; init; }
    public string? AccountTypeName { get; init; }
    public byte Status { get; init; }
    public DateTime ModifiedAt { get; init; }
    public int? PaymentMethodCode { get; init; }
    public string? AccountNumber { get; init; }
    public string? Clabe { get; init; }
}

public sealed class LegacyCustomerAccountReadRepository(IOptions<LegacySqlOptions> options, IExecutionTenantContext tenantContext) : ICustomerAccountReadRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;
    internal const string ReadSql = """
        SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPERSONA WHERE PNA_FL_PERSONA = @PersonId) THEN 1 ELSE 0 END AS CustomerExists;
        SELECT C.PCT_FL_CVE AS AccountId, C.PNA_FL_PERSONA AS PersonId, C.BCO_FL_CVE AS BankId,
               B.BCO_DS_NOMBRE AS BankName, C.PCT_NO_SUCURSAL AS BranchNumber,
               C.PCT_CL_MONEDA AS CurrencyCode, M.PAR_DS_DESCRIPCION AS CurrencyName,
               C.PCT_CL_TCUENTA AS AccountTypeCode, T.PAR_DS_DESCRIPCION AS AccountTypeName,
               C.PCT_FG_STATUS AS Status, C.PCT_FE_ULTMOD AS ModifiedAt,
               C.PCT_CL_MPAGO AS PaymentMethodCode, C.PCT_NO_CUENTA AS AccountNumber,
               C.PCT_NO_CLABE AS Clabe
        FROM dbo.CPCUENTA AS C
        LEFT JOIN dbo.CBANCO AS B ON B.BCO_FL_CVE = C.BCO_FL_CVE
        LEFT JOIN dbo.CPARAMETRO AS M ON M.PAR_FL_CVE = 4 AND M.PAR_CL_VALOR = C.PCT_CL_MONEDA AND M.PAR_FG_STATUS = 1
        LEFT JOIN dbo.CPARAMETRO AS T ON T.PAR_FL_CVE = 71 AND T.PAR_CL_VALOR = C.PCT_CL_TCUENTA AND T.PAR_FG_STATUS = 1
        WHERE C.PNA_FL_PERSONA = @PersonId
        ORDER BY C.PCT_FG_STATUS DESC, C.PCT_FL_CVE ASC;
        """;

    public async Task<IReadOnlyList<ManagedCustomerAccount>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        if (await _tenantContext.GetAsync(cancellationToken) is null) return null;
        await using var connection = new SqlConnection(_options.ReadConnectionString);
        await connection.OpenAsync(cancellationToken);
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(ReadSql, new { PersonId = personId }, commandTimeout: _options.CommandTimeoutSeconds, commandType: CommandType.Text, cancellationToken: cancellationToken));
        if (await result.ReadFirstAsync<int>() == 0) return null;
        return (await result.ReadAsync<LegacyCustomerAccountReadRow>()).Select(Map).ToArray();
    }

    private static ManagedCustomerAccount Map(LegacyCustomerAccountReadRow row) => new(
        row.AccountId, row.PersonId, row.BankId, row.BankName, row.BranchNumber,
        row.CurrencyCode, row.CurrencyName, row.AccountTypeCode, row.AccountTypeName,
        row.PaymentMethodCode, row.Status, Mask(row.AccountNumber), Mask(row.Clabe), row.ModifiedAt);

    private static string? Mask(string? value) => string.IsNullOrEmpty(value) ? null : value.Length >= 4 ? $"••••{value[^4..]}" : "••••";
}
