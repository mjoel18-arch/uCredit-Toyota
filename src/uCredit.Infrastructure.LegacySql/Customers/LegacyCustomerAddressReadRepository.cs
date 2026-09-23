using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

internal sealed class ManagedCustomerAddressReadRow
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

public sealed class LegacyCustomerAddressReadRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext tenantContext) : ICustomerAddressReadRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;

    internal const string ReadSql = """
        SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPERSONA WHERE PNA_FL_PERSONA = @PersonId) THEN 1 ELSE 0 END AS CustomerExists;
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
        WHERE D.PNA_FL_PERSONA = @PersonId
        ORDER BY D.DMO_FG_REGDEFAULT DESC, D.DMO_FL_CVE ASC;
        """;

    public async Task<IReadOnlyList<ManagedCustomerAddress>?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        if (await _tenantContext.GetAsync(cancellationToken) is null)
            return null;

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        await connection.OpenAsync(cancellationToken);
        var parameters = new DynamicParameters();
        parameters.Add("PersonId", personId, DbType.Int32);
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            ReadSql,
            parameters,
            commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        if (await result.ReadFirstAsync<int>() == 0)
            return null;

        return (await result.ReadAsync<ManagedCustomerAddressReadRow>())
            .Select(Map)
            .ToArray();
    }

    private static ManagedCustomerAddress Map(ManagedCustomerAddressReadRow row) => new(
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
        CustomerAddressUses(row),
        Require(row.IsActive, nameof(row.IsActive)) == 1,
        Require(row.IsDefault, nameof(row.IsDefault)) == 1,
        row.ModifiedAt ?? throw new InvalidOperationException("Legacy address modified date was unexpectedly null."),
        Require(row.CountryCode, nameof(row.CountryCode)));

    private static string[] CustomerAddressUses(ManagedCustomerAddressReadRow row) =>
        new[]
        {
            row.Billing == 1 ? CustomerAddressUse.Billing : null,
            row.Statements == 1 ? CustomerAddressUse.Statements : null,
            row.Other == 1 ? CustomerAddressUse.Other : null,
        }.OfType<string>().ToArray();

    private static int Require(int? value, string name) => value ?? throw new InvalidOperationException($"Legacy address column {name} was unexpectedly null.");
}
