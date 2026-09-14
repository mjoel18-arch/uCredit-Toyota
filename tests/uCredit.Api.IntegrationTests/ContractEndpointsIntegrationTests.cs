using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Api.IntegrationTests;

public sealed class ContractEndpointsIntegrationTests(TestApiFactory factory)
    : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task SearchWithoutAuthenticationReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/contracts/?contractNumber=CONTRACT-1", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchWithoutContractsReadPermissionReturnsForbidden()
    {
        var response = await CreateClient("without-permission").GetAsync("/api/v1/contracts/?contractNumber=CONTRACT-1", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
    }

    [Fact]
    public async Task GetWithContractsReadPermissionReturnsContract()
    {
        var response = await CreateClient("with-permission").GetAsync("/api/v1/contracts/CONTRACT-1", TestContext.Current.CancellationToken);
        var contract = await response.Content.ReadFromJsonAsync<ContractSummary>(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(contract);
        Assert.Equal("CONTRACT-1", contract.ContractNumber);
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