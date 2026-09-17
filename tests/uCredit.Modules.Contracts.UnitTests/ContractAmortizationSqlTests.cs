using System.Data;
using Dapper;
using UCredit.Infrastructure.LegacySql.Contracts;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Modules.Contracts.UnitTests;

public sealed class ContractAmortizationSqlTests
{
    [Fact]
    public void ScheduleSqlValidatesContractScopeSelectsLatestVersionAndDoesNotFilterGeneratedState()
    {
        const string maliciousContractNumber = "CONTRACT' OR 1=1 --";
        var sql = LegacyContractAmortizationReadRepository.CreateScheduleSql();

        Assert.Contains("FROM dbo.KTPAGO_CONTRATO AS P", sql, StringComparison.Ordinal);
        Assert.Contains("INNER JOIN dbo.KCONTRATO AS C", sql, StringComparison.Ordinal);
        Assert.Contains("C.EMP_FL_CVE IN @AllowedCompanyIds", sql, StringComparison.Ordinal);
        Assert.Contains("P.CTP_CL_TTABLA = @FinancingType", sql, StringComparison.Ordinal);
        Assert.Contains("MAX(V.CTP_NO_VERSION)", sql, StringComparison.Ordinal);
        Assert.Contains("V.CTP_CL_TTABLA = @FinancingType", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY P.CTP_NO_PAGO ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CTP_FG_GENERADO =", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(maliciousContractNumber, sql, StringComparison.Ordinal);
        Assert.Contains("@ContractNumber", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleParametersKeepContractAndCompanyScopeParameterized()
    {
        var parameters = LegacyContractAmortizationReadRepository.CreateParameters("  CONTRACT-1  ", [1, 2]);

        Assert.Equal("CONTRACT-1", parameters.Get<string>("ContractNumber"));
        Assert.Equal([1, 2], parameters.Get<int[]>("AllowedCompanyIds"));
        Assert.Equal(1, parameters.Get<int>("FinancingType"));
    }

    [Fact]
    public void MappingSeparatesDownPaymentAndTranslatesGeneratedStates()
    {
        var result = LegacyContractAmortizationReadRepository.MapRows(
        [
            CreateRow(100, 0, 4, 1, new DateTime(2025, 1, 1)),
            CreateRow(101, 1, 4, 1, new DateTime(2025, 2, 1)),
            CreateRow(102, 2, 4, 0, new DateTime(2025, 3, 1)),
        ]);

        Assert.NotNull(result);
        Assert.Equal("CONTRACT-1", result.ContractNumber);
        Assert.Equal(1, result.FinancingType);
        Assert.Equal(4, result.Version);
        Assert.NotNull(result.DownPayment);
        Assert.Equal(0, result.DownPayment.PaymentNumber);
        Assert.Equal(ContractAmortizationPaymentStatus.Generated, result.DownPayment.Status);
        Assert.Equal(2, result.Payments.Count);
        Assert.Equal([1, 2], result.Payments.Select(payment => payment.PaymentNumber));
        Assert.Equal(ContractAmortizationPaymentStatus.Generated, result.Payments[0].Status);
        Assert.Equal(ContractAmortizationPaymentStatus.Pending, result.Payments[1].Status);
        Assert.Equal(new DateOnly(2025, 2, 1), result.Payments[0].StartDate);
        Assert.Equal(new DateOnly(2025, 2, 28), result.Payments[0].EndDate);
        Assert.Equal(new DateOnly(2025, 2, 15), result.Payments[0].DueDate);
    }

    [Fact]
    public void MappingReturnsNullDownPaymentWhenPaymentZeroDoesNotExist()
    {
        var result = LegacyContractAmortizationReadRepository.MapRows(
        [
            CreateRow(101, 1, 4, 1, new DateTime(2025, 2, 1)),
        ]);

        Assert.NotNull(result);
        Assert.Null(result.DownPayment);
        Assert.Single(result.Payments);
        Assert.All(result.Payments, payment => Assert.True(payment.PaymentNumber > 0));
    }

    [Fact]
    public void MappingOrdersPaymentsByPaymentNumber()
    {
        var result = LegacyContractAmortizationReadRepository.MapRows(
        [
            CreateRow(102, 2, 4, 0, new DateTime(2025, 3, 1)),
            CreateRow(101, 1, 4, 1, new DateTime(2025, 2, 1)),
        ]);

        Assert.NotNull(result);
        Assert.Equal([1, 2], result.Payments.Select(payment => payment.PaymentNumber));
    }

    [Fact]
    public void EmptyScheduleMapsToNotFoundModel()
    {
        Assert.Null(LegacyContractAmortizationReadRepository.MapRows([]));
    }

    [Fact]
    public void MappingFailsFastWhenRequiredColumnIsNull()
    {
        var row = CreateRow(101, 1, 4, 1, new DateTime(2025, 2, 1));
        row.PaymentWithIva = null;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LegacyContractAmortizationReadRepository.MapRows([row]));

        Assert.Contains(nameof(LegacyContractAmortizationRow.PaymentWithIva), exception.Message, StringComparison.Ordinal);
    }

    private static LegacyContractAmortizationRow CreateRow(
        long paymentId,
        int paymentNumber,
        int version,
        int generatedFlag,
        DateTime startDate) => new()
        {
            PaymentId = paymentId,
            ContractNumber = "CONTRACT-1",
            PaymentNumber = paymentNumber,
            Version = version,
            GeneratedFlag = generatedFlag,
            StartDate = startDate,
            EndDate = startDate.AddDays(27),
            DueDate = startDate.AddDays(14),
            CalculationBase = 10000m,
            OutstandingBalance = 9000m,
            Amortization = 1000m,
            Interest = 500m,
            Iva = 80m,
            Payment = 1500m,
            PaymentWithIva = 1580m,
            TotalPayment = 1580m,
        };
}
