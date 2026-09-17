using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UCredit.Infrastructure.Identity.Tenants;

namespace UCredit.Api.IntegrationTests;

public sealed class TenantSelectionIntegrationTests(TestApiFactory factory)
    : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task SelectTenantWithSingleActiveMembershipReturnsItsPermissions()
    {
        var response = await SelectTenantAsync("single", "TENANT-A");
        var body = await response.Content.ReadFromJsonAsync<TenantSelectionResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("TENANT-A", body.TenantCode);
        Assert.Equal(["contracts.read"], body.Permissions);
    }

    [Fact]
    public async Task MeReturnsOnlyTheDeploymentTenantMembership()
    {
        using var client = CreateClient("multiple-memberships");
        var response = await client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("TENANT-A", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TENANT-B", json, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessFailedCount", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectTenantWithMultipleMembershipsUsesOnlyDeploymentTenantPermissions()
    {
        var response = await SelectTenantAsync("multiple-memberships", "TENANT-A");
        var body = await response.Content.ReadFromJsonAsync<TenantSelectionResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("TENANT-A", body.TenantCode);
        Assert.Equal(["contracts.read"], body.Permissions);
        Assert.DoesNotContain("contracts.write", body.Permissions);
    }

    [Fact]
    public async Task NonexistentTenantReturnsForbidden()
    {
        var response = await SelectTenantAsync("single", "DOES-NOT-EXIST");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InactiveTenantReturnsForbidden()
    {
        var response = await SelectTenantAsync("inactive-tenant", "TENANT-INACTIVE");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InactiveMembershipReturnsForbidden()
    {
        var response = await SelectTenantAsync("inactive-membership", "TENANT-INACTIVE-MEMBERSHIP");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ForeignTenantReturnsForbidden()
    {
        var response = await SelectTenantAsync("single", "TENANT-B");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangingTenantToAnotherTenantIsRejectedByDeployment()
    {
        await SelectTenantAsync("change-tenant", "TENANT-A");
        var response = await SelectTenantAsync("change-tenant", "TENANT-B");
        var issuer = factory.Services.GetRequiredService<FakeTenantCookieIssuer>();
        var claims = issuer.GetIssuedClaims(TestIdentityData.ChangeTenantUserId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(claims, claim => claim.Type == TenantClaimTypes.Code && claim.Value == "TENANT-A");
        Assert.Contains(claims, claim => claim.Type == "permission" && claim.Value == "contracts.read");
        Assert.DoesNotContain(claims, claim => claim.Type == "permission" && claim.Value == "contracts.write");
    }

    [Fact]
    public async Task InactiveUserCannotSelectTenant()
    {
        var response = await SelectTenantAsync("inactive-user", "TENANT-A");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SelectTenantWithoutAntiforgeryTokenReturnsBadRequest()
    {
        using var client = CreateClient("single");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/select-tenant")
        {
            Content = JsonContent.Create(new { tenantCode = "TENANT-A" })
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TenantHeaderDoesNotOverrideSignedTenantAuthority()
    {
        using var client = CreateClient("single");
        client.DefaultRequestHeaders.Add("X-Tenant-Code", "TENANT-B");

        var response = await client.GetAsync("/api/v1/branding/current", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("UCREDIT", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TENANT-B", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToyotaBrandingUsesSignedTenantAndExposesNoSecrets()
    {
        using var client = CreateClient("toyota");
        client.DefaultRequestHeaders.Add("X-Tenant-Code", "TENANT-B");

        var response = await client.GetAsync("/api/v1/branding/current", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("uCredit-auto", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        var branding = document.RootElement;
        Assert.Equal("uCredit-auto", branding.GetProperty("productName").GetString());
        Assert.Equal("Toyota Financial Services", branding.GetProperty("customerName").GetString());
        Assert.Equal("/branding/toyota/logo.png", branding.GetProperty("logoUrl").GetString());
        Assert.Equal("uCredit-auto | Toyota Financial Services", branding.GetProperty("browserTitle").GetString());
        Assert.Contains("TOYOTA", json, StringComparison.Ordinal);
        Assert.Contains("primaryColor", json, StringComparison.Ordinal);
        Assert.Contains("secondaryColor", json, StringComparison.Ordinal);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);

        using var defaultClient = CreateClient("single");
        var defaultResponse = await defaultClient.GetAsync("/api/v1/branding/current", TestContext.Current.CancellationToken);
        var defaultJson = await defaultResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        Assert.Contains("uCredit", defaultJson, StringComparison.Ordinal);
        Assert.DoesNotContain("uCredit-auto", defaultJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Toyota Financial Services", defaultJson, StringComparison.Ordinal);
    }

    private HttpClient CreateClient(string mode)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, mode);
        return client;
    }

    private async Task<HttpResponseMessage> SelectTenantAsync(
        string mode,
        string tenantCode)
    {
        using var client = CreateClient(mode);
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf", TestContext.Current.CancellationToken);
        using var csrfDocument = JsonDocument.Parse(
            await csrfResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var csrf = csrfDocument.RootElement.GetProperty("requestToken").GetString()!;
        var headerName = csrfDocument.RootElement.GetProperty("headerName").GetString()!;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/select-tenant")
        {
            Content = JsonContent.Create(new { tenantCode })
        };
        request.Headers.Add(headerName, csrf);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed record TenantSelectionResponse(
        Guid TenantId,
        string TenantCode,
        string[] Permissions);
}
