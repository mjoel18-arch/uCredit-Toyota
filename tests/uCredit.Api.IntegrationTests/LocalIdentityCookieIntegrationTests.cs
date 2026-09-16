using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UCredit.Api.IntegrationTests;

public sealed class LocalIdentityCookieIntegrationTests(LocalIdentityApiFactory factory)
    : IClassFixture<LocalIdentityApiFactory>
{
    [Fact]
    public async Task LoginLogoutThenMeReturnsUnauthorizedWithoutRedirect()
    {
        await factory.SeedAdminAsync();
        using var client = CreateClient(factory);

        var loginCsrf = await GetCsrfAsync(client);
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                userName = LocalIdentityApiFactory.UserName,
                password = LocalIdentityApiFactory.Password
            })
        };
        loginRequest.Headers.Add(loginCsrf.HeaderName, loginCsrf.RequestToken);
        var loginResponse = await client.SendAsync(loginRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Null(loginResponse.Headers.Location);

        var meBeforeTenant = await client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, meBeforeTenant.StatusCode);
        Assert.Null(meBeforeTenant.Headers.Location);

        var logoutCsrf = await GetCsrfAsync(client);
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Add(logoutCsrf.HeaderName, logoutCsrf.RequestToken);
        var logoutResponse = await client.SendAsync(logoutRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Null(logoutResponse.Headers.Location);

        var afterLogout = await client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
        Assert.Null(afterLogout.Headers.Location);
    }

    private static HttpClient CreateClient(LocalIdentityApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<CsrfResponse> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/csrf", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<CsrfResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        return body;
    }

    private sealed record CsrfResponse(
        [property: JsonPropertyName("requestToken")] string RequestToken,
        [property: JsonPropertyName("headerName")] string HeaderName);
}
