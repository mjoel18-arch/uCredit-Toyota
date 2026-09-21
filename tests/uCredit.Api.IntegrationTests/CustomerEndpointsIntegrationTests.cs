using System.Net;
using System.Net.Http.Json;

namespace UCredit.Api.IntegrationTests;

public sealed class CustomerEndpointsIntegrationTests(TestApiFactory factory)
    : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task AnonymousCustomerSearchReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/customers/?personId=42", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CustomerSearchWithoutPermissionReturnsForbidden()
    {
        using var client = CreateClient("with-permission");
        var response = await client.GetAsync("/api/v1/customers/?personId=42", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerSearchWithPermissionReturnsPagedCustomer()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/customers/?personId=42&page=1&pageSize=20", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<CustomerPage>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var customer = Assert.Single(body.Items);
        Assert.Equal(42, customer.PersonId);
        Assert.Equal("ABC0******B1", customer.RfcMasked);
        Assert.Equal("FISICA", customer.LegalPersonality.Description);
        Assert.Equal("CLIENTE", Assert.Single(customer.Roles).Description);
    }

    [Fact]
    public async Task CustomerSearchWithoutCriteriaReturnsBadRequest()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/customers/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingCustomerReturnsNotFound()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/customers/999", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CustomerDetailReturnsActivePhonesAndEmails()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/customers/42", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<CustomerDetail>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("ABC010203AB1", body.Rfc);
        Assert.Equal(2, body.ActivePhones.Length);
        Assert.Equal("5555555555", body.PrimaryPhone!.PhoneNumber);
        Assert.Single(body.ActiveEmails);
    }

    [Fact]
    public async Task AnonymousPhoneListReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/customers/42/phones", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PhoneListWithoutPermissionReturnsForbidden()
    {
        using var client = CreateClient("with-permission");
        var response = await client.GetAsync("/api/v1/customers/42/phones", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PhoneListWithPermissionReturnsHistoricalAndActivePhones()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/customers/42/phones", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<PhoneResponse[]>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Length);
        Assert.Equal("Active", body[0].Status);
        Assert.Equal("InheritedInactive", body[1].Status);
    }

    [Fact]
    public async Task CustomerReadinessReturnsCompleteProfileWithoutAccountData()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/customers/42/readiness", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<CustomerReadiness>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.CanCreateContract);
        Assert.Empty(body.MissingRequirements);
        Assert.DoesNotContain("accountNumber", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clabe", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(43, "address")]
    [InlineData(44, "phone")]
    [InlineData(45, "account")]
    public async Task CustomerReadinessReturnsMissingRequirement(int personId, string missing)
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync($"/api/v1/customers/{personId}/readiness", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<CustomerReadiness>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.CanCreateContract);
        Assert.Equal([missing], body.MissingRequirements);
    }

    [Fact]
    public async Task CustomerReadinessReturnsMultipleMissingRequirements()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/customers/46/readiness", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<CustomerReadiness>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(["address", "phone", "account"], body.MissingRequirements);
    }

    [Fact]
    public async Task CustomerReadinessRequiresTenantSelection()
    {
        using var client = CreateClient("customers-without-tenant");
        var response = await client.GetAsync("/api/v1/customers/42/readiness", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CustomerReadinessRequiresPermission()
    {
        using var client = CreateClient("with-permission");
        var response = await client.GetAsync("/api/v1/customers/42/readiness", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerReadinessMissingPersonReturnsNotFound()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/customers/999/readiness", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient(string mode)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, mode);
        return client;
    }

    private sealed record CustomerPage(CustomerList[] Items, int Page, int PageSize, int Total);
    private sealed record CustomerList(int PersonId, string? RfcMasked, CustomerPersonality LegalPersonality, CustomerRole[] Roles);
    private sealed record CustomerPersonality(int Code, string? Description);
    private sealed record CustomerRole(int Code, string? Description);
    private sealed record CustomerDetail(string? Rfc, CustomerPhone? PrimaryPhone, CustomerPhone[] ActivePhones, CustomerEmail[] ActiveEmails);
    private sealed record CustomerPhone(int PhoneId, string? AreaCode, string? PhoneNumber, string? Extension, bool IsDefault);
    private sealed record CustomerEmail(int EmailId, string? Contact, string? Email);
    private sealed record CustomerReadiness(int PersonId, bool HasGeneralData, bool HasAddress, bool HasPhone, bool HasAccount, bool CanCreateContract, string[] MissingRequirements);
    private sealed record PhoneResponse(int PhoneId, string Status, bool IsDefault);
}
