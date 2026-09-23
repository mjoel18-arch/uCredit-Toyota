using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UCredit.Api.IntegrationTests;

public sealed class ContractEndpointsIntegrationTests(TestApiFactory factory)
    : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task AddressCatalogReturnsOnlySafeActiveOptions()
    {
        var response = await CreateClient("customers-with-permission").GetAsync(
            "/api/v1/contracts/catalogs/addresses?personId=42",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var addresses = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, addresses.Length);
        Assert.Equal(7, addresses[0].GetProperty("id").GetInt32());
        Assert.Equal("Dirección única", addresses[0].GetProperty("typeDescription").GetString());
        Assert.False(addresses[0].TryGetProperty("personId", out _));
        Assert.False(addresses[0].TryGetProperty("postalCode", out _));
    }

    [Fact]
    public async Task AddressCatalogReturnsEmptyForCustomerWithoutActiveAddresses()
    {
        var response = await CreateClient("customers-with-permission").GetAsync(
            "/api/v1/contracts/catalogs/addresses?personId=43",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddressCatalogReturnsNotFoundForUnknownCustomer()
    {
        var response = await CreateClient("customers-with-permission").GetAsync(
            "/api/v1/contracts/catalogs/addresses?personId=999",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AddressCatalogRequiresAuthenticationAndContractsReadPermission()
    {
        var anonymous = await factory.CreateClient().GetAsync(
            "/api/v1/contracts/catalogs/addresses?personId=42",
            TestContext.Current.CancellationToken);
        var forbidden = await CreateClient("without-permission").GetAsync(
            "/api/v1/contracts/catalogs/addresses?personId=42",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Null(anonymous.Headers.Location);
        Assert.Null(forbidden.Headers.Location);
    }

    [Fact]
    public async Task AddressCatalogRejectsASelectedTenantThatDoesNotMatchDeployment()
    {
        var response = await CreateClient("contracts-invalid-tenant").GetAsync(
            "/api/v1/contracts/catalogs/addresses?personId=42",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AddressCatalogKeepsUnexpectedErrorsAsInternalServerError()
    {
        var response = await CreateClient("customers-with-permission").GetAsync(
            "/api/v1/contracts/catalogs/addresses?personId=500",
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("unavailable", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Synthetic catalog failure", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchBy562947CdReturnsTheRequestedPagedResult()
    {
        var response = await CreateClient("with-permission").GetAsync(
            "/api/v1/contracts/?contractNumber=562947CD&page=1&pageSize=10",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, document.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal("562947CD", document.RootElement.GetProperty("items")[0].GetProperty("contractNumber").GetString());
    }

    [Fact]
    public async Task Get562947CdOpensTheContractDetailWithoutFoundationDependencies()
    {
        var response = await CreateClient("with-permission").GetAsync(
            "/api/v1/contracts/562947CD",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("562947CD", document.RootElement.GetProperty("contractNumber").GetString());
    }

    [Fact]
    public async Task MissingContractStillReturnsNotFoundAndCatalogRoutesRemainSeparate()
    {
        var missing = await CreateClient("with-permission").GetAsync(
            "/api/v1/contracts/does-not-exist",
            TestContext.Current.CancellationToken);
        var addresses = await CreateClient("customers-with-permission").GetAsync(
            "/api/v1/contracts/catalogs/addresses?personId=43",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, addresses.StatusCode);
    }

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
    public async Task AmortizationWithoutDownPaymentReturnsNullAndPositivePaymentsOnly()
    {
        var response = await CreateClient("with-permission").GetAsync(
            "/api/v1/contracts/NODOWNPAY-001/amortization-schedule",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("downPayment").ValueKind);
        Assert.All(root.GetProperty("payments").EnumerateArray(), payment =>
            Assert.True(payment.GetProperty("paymentNumber").GetInt32() > 0));
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
    public async Task AmortizationWithoutFinancingScheduleReturnsNotFound()
    {
        var response = await CreateClient("with-permission").GetAsync(
            "/api/v1/contracts/NOAMORT-001/amortization-schedule",
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
