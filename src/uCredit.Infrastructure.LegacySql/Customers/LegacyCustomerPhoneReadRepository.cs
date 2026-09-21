using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

internal sealed class ManagedCustomerPhoneReadRow
{
    public int? PhoneId { get; set; }
    public int? PersonId { get; set; }
    public int? PhoneTypeCode { get; set; }
    public int? AddressId { get; set; }
    public string? LongDistanceCode { get; set; }
    public string? AreaCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Extension { get; set; }
    public int? StatusCode { get; set; }
    public string? InactiveReason { get; set; }
    public int? IsDefault { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ContactName { get; set; }
}

public sealed class LegacyCustomerPhoneReadRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext tenantContext) : ICustomerPhoneReadRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;

    internal const string ReadSql = """
        SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPERSONA WHERE PNA_FL_PERSONA = @PersonId) THEN 1 ELSE 0 END AS CustomerExists;
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
        WHERE T.PNA_FL_PERSONA = @PersonId
        ORDER BY T.TFN_FG_STATUS DESC, T.TFN_FG_REGDEFAULT DESC, T.TFN_FL_CVE ASC;
        """;

    public async Task<IReadOnlyList<ManagedCustomerPhone>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        if (await _tenantContext.GetAsync(cancellationToken) is null)
            return null;

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        await connection.OpenAsync(cancellationToken);
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            ReadSql,
            new { PersonId = personId },
            commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        if (await result.ReadFirstAsync<int>() == 0)
            return null;

        return (await result.ReadAsync<ManagedCustomerPhoneReadRow>()).Select(Map).ToArray();
    }

    private static ManagedCustomerPhone Map(ManagedCustomerPhoneReadRow row) => new(
        Require(row.PhoneId, nameof(row.PhoneId)),
        Require(row.PersonId, nameof(row.PersonId)),
        Require(row.PhoneTypeCode, nameof(row.PhoneTypeCode)),
        Require(row.AddressId, nameof(row.AddressId)),
        row.LongDistanceCode,
        row.AreaCode,
        row.PhoneNumber,
        row.Extension,
        Require(row.StatusCode, nameof(row.StatusCode)),
        row.InactiveReason,
        Require(row.IsDefault, nameof(row.IsDefault)) == 1,
        row.ModifiedAt ?? throw new InvalidOperationException("Legacy phone modified date was unexpectedly null."),
        row.ContactName);

    private static int Require(int? value, string name) => value ?? throw new InvalidOperationException($"Legacy phone column {name} was unexpectedly null.");
}
