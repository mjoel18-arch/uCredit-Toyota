using System.Net;
using System.Net.Http.Json;

namespace UCredit.Api.IntegrationTests;

public sealed class CustomerGeneralIntegrationTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private static readonly int[] PhysicalRoles = [1, 25];
    private static readonly int[] InsurerRole = [1, 10];
    private static readonly int[] OnlyClientRole = [1];
    private static readonly int[] DuplicateRoles = [1, 1];
    private static readonly int[] UnknownRoles = [999];

    [Theory]
    [InlineData(42)]
    [InlineData(53)]
    [InlineData(54)]
    public async Task GetGeneralReturnsPhysicalBusinessOrMoralProfile(int personId)
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync($"/api/v1/customers/{personId}/general", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("personId", json, StringComparison.Ordinal);
        Assert.Contains("roles", json, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyUserCode", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USR_CL_CVE", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetGeneralPreservesHistoricalRoleAndBlocksInsurerRole()
    {
        using var client = CreateClient("customers-with-permission");
        var historical = await client.GetStringAsync("/api/v1/customers/55/general", TestContext.Current.CancellationToken);
        var insurer = await client.GetStringAsync("/api/v1/customers/60/general", TestContext.Current.CancellationToken);

        Assert.Contains("ROL HISTORICO", historical, StringComparison.Ordinal);
        Assert.Contains("\"isActive\":false", historical, StringComparison.Ordinal);
        Assert.Contains("\"roleCode\":10", insurer, StringComparison.Ordinal);
        Assert.Contains("\"isEditable\":false", insurer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetGeneralEnforcesAuthenticationPermissionTenantAndNotFound()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/customers/42/general", TestContext.Current.CancellationToken)).StatusCode);
        using var noPermission = CreateClient("with-permission");
        using var noTenant = CreateClient("customers-without-tenant");
        using var permitted = CreateClient("customers-with-permission");
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync("/api/v1/customers/42/general", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noTenant.GetAsync("/api/v1/customers/42/general", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await permitted.GetAsync("/api/v1/customers/999/general", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task PutGeneralSupportsPhysicalMoralContactAndOmittedRoles()
    {
        using var client = CreateClient("customers-write");
        var csrf = await GetCsrfAsync(client);
        async Task<HttpResponseMessage> Put(int personId, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/customers/{personId}/general") { Content = JsonContent.Create(body) };
            request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var physical = await Put(42, new { firstName = "Nombre actualizado", roleCodes = PhysicalRoles, expectedPersonModifiedAt = "2025-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z", pepConfirmed = false });
        var moral = await Put(54, new { contactName = "Contacto actualizado", contactPosition = "Puesto actualizado", expectedPersonModifiedAt = "2025-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z", pepConfirmed = false });

        Assert.Equal(HttpStatusCode.OK, physical.StatusCode);
        Assert.Equal(HttpStatusCode.OK, moral.StatusCode);
    }

    [Fact]
    public async Task PutGeneralRejectsInvalidRolesImmutableFieldsAndConcurrencyConflicts()
    {
        using var client = CreateClient("customers-write");
        var csrf = await GetCsrfAsync(client);
        async Task<HttpResponseMessage> Put(int personId, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/customers/{personId}/general") { Content = JsonContent.Create(body) };
            request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var zero = await Put(42, new { roleCodes = Array.Empty<int>(), expectedPersonModifiedAt = "2025-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z", pepConfirmed = false });
        var duplicate = await Put(42, new { roleCodes = DuplicateRoles, expectedPersonModifiedAt = "2025-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z", pepConfirmed = false });
        var addInsurer = await Put(42, new { roleCodes = InsurerRole, expectedPersonModifiedAt = "2025-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z", pepConfirmed = false });
        var unknown = await Put(42, new { roleCodes = UnknownRoles, expectedPersonModifiedAt = "2025-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z", pepConfirmed = false });
        var removeInsurer = await Put(60, new { roleCodes = OnlyClientRole, expectedPersonModifiedAt = "2025-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z", pepConfirmed = false });
        var immutable = await Put(42, new { rfc = "FORBIDDEN", expectedPersonModifiedAt = "2025-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z", pepConfirmed = false });
        var conflict = await Put(56, new { firstName = "Cambio", expectedPersonModifiedAt = "2020-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z", pepConfirmed = false });
        var subtypeConflict = await Put(57, new { firstName = "Cambio", expectedPersonModifiedAt = "2025-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2020-01-01T00:00:00Z", pepConfirmed = false });

        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, addInsurer.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, removeInsurer.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, immutable.StatusCode);
        Assert.Contains("customer_modified", await conflict.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Contains("customer_modified", await subtypeConflict.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PutGeneralRequiresWriteAndAntiforgeryAndReturnsUnavailableSafely()
    {
        using var readOnly = CreateClient("customers-with-permission");
        var body = new { firstName = "Cambio", expectedPersonModifiedAt = "2025-01-01T00:00:00Z", expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z", pepConfirmed = false };
        Assert.Equal(HttpStatusCode.Forbidden, (await readOnly.PutAsJsonAsync("/api/v1/customers/42/general", body, TestContext.Current.CancellationToken)).StatusCode);

        using var client = CreateClient("customers-write");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/customers/42/general", body, TestContext.Current.CancellationToken)).StatusCode);
        var csrf = await GetCsrfAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/customers/58/general") { Content = JsonContent.Create(body) };
        request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        var unavailable = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.DoesNotContain("Cambio", await unavailable.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PutGeneralAppliesPepMatchNoMatchAndUnavailableRules()
    {
        using var client = CreateClient("customers-write");
        var csrf = await GetCsrfAsync(client);

        async Task<HttpResponseMessage> Put(int personId, bool pepConfirmed)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/customers/{personId}/general")
            {
                Content = JsonContent.Create(new
                {
                    firstName = "Cambio",
                    expectedPersonModifiedAt = "2025-01-01T00:00:00Z",
                    expectedSubtypeModifiedAt = "2025-01-02T00:00:00Z",
                    pepConfirmed,
                })
            };
            request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.OK, (await Put(42, false)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Put(61, false)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Put(61, true)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Put(62, true)).StatusCode);
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

    private sealed record CsrfResponse(string RequestToken, string HeaderName);
}
