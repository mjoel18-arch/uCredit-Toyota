using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UCredit.Api.IntegrationTests;

public sealed class ContractEndpointsIntegrationTests(TestApiFactory factory)
    : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task SearchWithoutAuthenticationReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/contracts/?contractNumber=CONTRACT-1", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task SearchWithoutContractsReadPermissionReturnsForbidden()
    {
        var response = await CreateClient("without-permission").GetAsync("/api/v1/contracts/?contractNumber=CONTRACT-1", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task SearchWithContractsReadPermissionReturnsOk()
    {
        var response = await CreateClient("with-permission").GetAsync("/api/v1/contracts/?contractNumber=CONTRACT-1", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWithoutAuthenticationReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/contracts/CONTRACT-1", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task GetWithContractsReadPermissionReturnsExpectedDetailDtoWithoutInternalFields()
    {
        var response = await CreateClient("with-permission").GetAsync("/api/v1/contracts/CONTRACT-1", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("CONTRACT-1", root.GetProperty("contractNumber").GetString());
        Assert.Equal("Active", root.GetProperty("status").GetProperty("description").GetString());
        Assert.Equal("Loan", root.GetProperty("operationType").GetProperty("description").GetString());
        Assert.Equal("Integration Test Customer", root.GetProperty("customerName").GetString());
        Assert.Equal(1000m, root.GetProperty("financedAmount").GetDecimal());
        Assert.Equal(750m, root.GetProperty("outstandingBalance").GetDecimal());
        Assert.Equal("MXN", root.GetProperty("currencyCode").GetString());
        Assert.Equal("PESO MEXICANO", root.GetProperty("currencyName").GetString());
        Assert.Equal(48, root.GetProperty("currentTerm").GetInt32());
        Assert.Equal(60, root.GetProperty("originalTerm").GetInt32());
        Assert.Equal("2025-12-15", root.GetProperty("startDate").GetString());
        Assert.Equal("2025-12-20", root.GetProperty("activationDate").GetString());
        Assert.False(root.TryGetProperty("personId", out _));
        Assert.False(root.TryGetProperty("addressId", out _));
        Assert.False(root.TryGetProperty("modifiedBy", out _));
        Assert.False(root.TryGetProperty("modifiedAt", out _));
    }

[Fact]
    public async Task AmortizationWithoutAuthenticationReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync(
            "/api/v1/contracts/CONTRACT-1/amortization-schedule",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AmortizationWithoutContractsReadPermissionReturnsForbidden()
    {
        var response = await CreateClient("without-permission").GetAsync(
            "/api/v1/contracts/CONTRACT-1/amortization-schedule",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AmortizationWithContractsReadPermissionReturnsCurrentVersionAndPaymentStates()
    {
        var response = await CreateClient("with-permission").GetAsync(
            "/api/v1/contracts/CONTRACT-1/amortization-schedule",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;
        var payments = root.GetProperty("payments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("CONTRACT-1", root.GetProperty("contractNumber").GetString());
        Assert.Equal(1, root.GetProperty("financingType").GetInt32());
        Assert.Equal(4, root.GetProperty("version").GetInt32());
        Assert.Equal(0, root.GetProperty("downPayment").GetProperty("paymentNumber").GetInt32());
        Assert.Equal(2, payments.GetArrayLength());
        Assert.Equal(1, payments[0].GetProperty("paymentNumber").GetInt32());
        Assert.Equal("Generated", payments[0].GetProperty("status").GetString());
        Assert.Equal("Pending", payments[1].GetProperty("status").GetString());
        Assert.DoesNotContain("paymentId", document.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AmortizationMissingContractReturnsNotFound()
    {
        var response = await CreateClient("with-permission").GetAsync(
            "/api/v1/contracts/MISSING/amortization-schedule",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task GetMissingContractReturnsNotFound()
    {
        var response = await CreateClient("with-permission").GetAsync("/api/v1/contracts/MISSING", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInvalidContractNumberReturnsValidationProblem()
    {
        var response = await CreateClient("with-permission").GetAsync("/api/v1/contracts/1234567890123456", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SearchWithInvalidCriteriaReturnsValidationProblem()
    {
        var response = await CreateClient("with-permission").GetAsync("/api/v1/contracts/?pageSize=1", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SearchWithContractsReadPermissionReturnsExpectedPagedJson()
    {
        var response = await CreateClient("with-permission").GetAsync("/api/v1/contracts/?contractNumber=CONTRACT-1&page=1&pageSize=10", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, root.GetProperty("total").GetInt32());
        Assert.Equal("CONTRACT-1", root.GetProperty("items")[0].GetProperty("contractNumber").GetString());
    }

    private HttpClient CreateClient(string mode)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, mode);
        return client;
    }
}
