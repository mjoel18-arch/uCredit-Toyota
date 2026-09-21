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

    [Fact]
    public async Task AccountListReturnsEmptyAndDoesNotExposeSensitiveProperties()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/customers/47/accounts", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", json);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AccountListReturnsMaskedValuesOnly()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/customers/42/accounts", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("••••0001", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"accountNumber\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"clabe\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("123456", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountMutationsRequireWritePermissionAndAntiforgery()
    {
        using var readOnlyClient = CreateClient("customers-with-permission");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/42/accounts")
        {
            Content = JsonContent.Create(new { bankId = 2, branchNumber = 1, currencyCode = 1, accountTypeCode = 1, accountNumber = "0000000001", clabe = "000000000000000001" })
        };
        var forbidden = await readOnlyClient.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var writeClient = CreateClient("customers-write");
        var missingToken = await writeClient.PostAsJsonAsync("/api/v1/customers/42/accounts", new { bankId = 2, branchNumber = 1, currencyCode = 1, accountTypeCode = 1, accountNumber = "0000000001", clabe = "000000000000000001" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
    }

    [Fact]
    public async Task AccountCreateReturns201WithoutSensitiveResponseValues()
    {
        using var client = CreateClient("customers-write");
        var csrf = await GetCsrfAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/42/accounts")
        {
            Content = JsonContent.Create(new { bankId = 2, branchNumber = 1, currencyCode = 1, accountTypeCode = 1, accountNumber = "0000000001", clabe = "000000000000000001" })
        };
        request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("0000000001", json, StringComparison.Ordinal);
        Assert.DoesNotContain("000000000000000001", json, StringComparison.Ordinal);
        Assert.Contains("maskedAccountNumber", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountConflictAndUnavailableResponsesAreControlled()
    {
        using var client = CreateClient("customers-write");
        var csrf = await GetCsrfAsync(client);
        async Task<HttpResponseMessage> PostFor(int personId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/customers/{personId}/accounts")
            {
                Content = JsonContent.Create(new { bankId = 2, branchNumber = 1, currencyCode = 1, accountTypeCode = 1, accountNumber = "0000000001", clabe = "000000000000000001" })
            };
            request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var duplicate = await PostFor(48);
        var unavailable = await PostFor(50);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Contains("account_duplicate", await duplicate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Null(duplicate.Headers.Location);
        Assert.Null(unavailable.Headers.Location);

        using var modifiedRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/customers/49/accounts/7001")
        {
            Content = JsonContent.Create(new { bankId = 2, branchNumber = 12, currencyCode = 1, accountTypeCode = 1, accountNumber = (string?)null, clabe = (string?)null, expectedModifiedAt = "2025-01-01T00:00:00Z" })
        };
        modifiedRequest.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        var modified = await client.SendAsync(modifiedRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, modified.StatusCode);
        Assert.Contains("account_modified", await modified.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountUpdateAndStateEndpointsReturnSuccessfulResponses()
    {
        using var client = CreateClient("customers-write");
        var csrf = await GetCsrfAsync(client);
        var expected = "2025-01-01T00:00:00Z";

        async Task<HttpResponseMessage> Send(HttpMethod method, string path, object body)
        {
            using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
            request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var update = await Send(HttpMethod.Put, "/api/v1/customers/42/accounts/7001", new { bankId = 2, branchNumber = 12, currencyCode = 1, accountTypeCode = 1, accountNumber = (string?)null, clabe = (string?)null, expectedModifiedAt = expected });
        var activate = await Send(HttpMethod.Post, "/api/v1/customers/42/accounts/7001/activate", new { expectedModifiedAt = expected });
        var deactivate = await Send(HttpMethod.Post, "/api/v1/customers/42/accounts/7001/deactivate", new { expectedModifiedAt = expected });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Null(update.Headers.Location);
        Assert.Null(activate.Headers.Location);
        Assert.Null(deactivate.Headers.Location);
    }

    [Fact]
    public async Task AccountInvalidPayloadAndMissingPersonReturnControlledErrors()
    {
        using var client = CreateClient("customers-write");
        var csrf = await GetCsrfAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers/42/accounts") { Content = JsonContent.Create(new { bankId = 2, branchNumber = 1, currencyCode = 1, accountTypeCode = 1 }) };
        request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        var badRequest = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var missing = await client.GetAsync("/api/v1/customers/999/accounts", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task AccountGetRequiresAuthenticationAndTenant()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/v1/customers/42/accounts", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var noTenant = CreateClient("customers-without-tenant");
        var invalidTenant = await noTenant.GetAsync("/api/v1/customers/42/accounts", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, invalidTenant.StatusCode);
    }

    [Fact]
    public async Task BankCatalogReturnsOnlyApprovedSortedFields()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/catalogs/banks", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Banco Alfa", json, StringComparison.Ordinal);
        Assert.True(json.IndexOf("Banco Alfa", StringComparison.Ordinal) < json.IndexOf("Banco Beta", StringComparison.Ordinal));
        Assert.DoesNotContain("BCO_FG_STATUS", json, StringComparison.Ordinal);
        Assert.DoesNotContain("BCO_FG_REAL", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PCT_NO_CUENTA", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PCT_NO_CLABE", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BankCatalogRequiresAuthenticationAndSelectedTenant()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/v1/catalogs/banks", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var noPermission = CreateClient("with-permission");
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync("/api/v1/catalogs/banks", TestContext.Current.CancellationToken)).StatusCode);

        using var noTenant = CreateClient("customers-without-tenant");
        Assert.Equal(HttpStatusCode.Forbidden, (await noTenant.GetAsync("/api/v1/catalogs/banks", TestContext.Current.CancellationToken)).StatusCode);
    }

    private HttpClient CreateClient(string mode)
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, mode);
        return client;
    }

    private static async Task<CsrfResponse> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/csrf", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CsrfResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        return body;
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
    private sealed record CsrfResponse(string RequestToken, string HeaderName);
}
