using System.Net;
using System.Net.Http.Json;

namespace UCredit.Api.IntegrationTests;

public sealed class CustomerEndpointsIntegrationTests(TestApiFactory factory)
    : IClassFixture<TestApiFactory>
{
    private static readonly int[] InvoiceUse = [1];
    private static readonly int[] InvoiceAndStatementsUses = [1, 2];
    private static readonly int[] StatementsUse = [2];
    private static readonly int[] UnknownUse = [99];
    private static readonly int[] DuplicateUses = [1, 1];
    private static readonly int[] InactiveUse = [4];

    [Fact]
    public async Task CustomerCreateRejectsLegacyEmailProperties()
    {
        using var client = CreateClient("customers-write");
        var csrf = await GetCsrfAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers")
        {
            Content = JsonContent.Create(new
            {
                legalPersonality = 1,
                rfc = "AAA010101AAA",
                firstName = "Synthetic",
                paternalSurname = "Person",
                maternalSurname = "Test",
                constitutionOrBirthDate = "1980-01-01",
                countryCode = 1,
                groupCode = 1,
                riskCode = 1,
                contactFormCode = 1,
                taxRegimeCode = "605",
                phoneTypeCode = 1,
                areaCode = "55",
                phoneNumber = "5555555555",
                phoneContact = (string?)null,
                pepConfirmed = false,
                email = "legacy@example.invalid"
            })
        };
        request.Headers.Add(csrf.HeaderName, csrf.RequestToken);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PersonRoleCatalogReturnsOnlyActiveRoleFields()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/catalogs/person-roles", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("roleCode", json, StringComparison.Ordinal);
        Assert.Contains("roleName", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PAR_FL_CVE", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PAR_FG_STATUS", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
public async Task PersonRoleCatalogRequiresAuthenticationPermissionAndTenant()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/v1/catalogs/person-roles", TestContext.Current.CancellationToken);
        using var noPermission = CreateClient("with-permission");
        using var noTenant = CreateClient("customers-without-tenant");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync("/api/v1/catalogs/person-roles", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noTenant.GetAsync("/api/v1/catalogs/person-roles", TestContext.Current.CancellationToken)).StatusCode);
    }

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
        Assert.DoesNotContain("cliente@example.test", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
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

    [Fact]
    public async Task EmailListReturnsEmptyAndMultipleEmailsWithUsages()
    {
        using var client = CreateClient("customers-with-permission");
        var empty = await client.GetAsync("/api/v1/customers/47/emails", TestContext.Current.CancellationToken);
        var multiple = await client.GetAsync("/api/v1/customers/42/emails", TestContext.Current.CancellationToken);
        var json = await multiple.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal("[]", await empty.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, multiple.StatusCode);
        Assert.Contains("uno@example.invalid", json, StringComparison.Ordinal);
        Assert.Contains("usageCodes", json, StringComparison.Ordinal);
        Assert.DoesNotContain("MAI_", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MAI_FG_OMITIR_ENVIO", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USR_CL_CVE", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailListRequiresAuthenticationPermissionTenantAndExistingCustomer()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/v1/customers/42/emails", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var noPermission = CreateClient("with-permission");
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync("/api/v1/customers/42/emails", TestContext.Current.CancellationToken)).StatusCode);

        using var noTenant = CreateClient("customers-without-tenant");
        Assert.Equal(HttpStatusCode.Forbidden, (await noTenant.GetAsync("/api/v1/customers/42/emails", TestContext.Current.CancellationToken)).StatusCode);

        using var client = CreateClient("customers-with-permission");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/customers/999/emails", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task EmailCatalogReturnsOnlyActiveUsagesAndNeverBlacklistOrLegacyColumns()
    {
        using var client = CreateClient("customers-with-permission");
        var response = await client.GetAsync("/api/v1/catalogs/email-uses", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.IndexOf("Envío de facturas", StringComparison.Ordinal) < json.IndexOf("Salesforce", StringComparison.Ordinal));
        Assert.DoesNotContain("248", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PAR_FL_CVE", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("direccion", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailCatalogRequiresAuthenticationPermissionAndTenant()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/v1/catalogs/email-uses", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var noPermission = CreateClient("with-permission");
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync("/api/v1/catalogs/email-uses", TestContext.Current.CancellationToken)).StatusCode);

        using var noTenant = CreateClient("customers-without-tenant");
        Assert.Equal(HttpStatusCode.Forbidden, (await noTenant.GetAsync("/api/v1/catalogs/email-uses", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task EmailCreateUpdateAndStateEndpointsEnforceWriteAndAntiforgery()
    {
        using var readOnlyClient = CreateClient("customers-with-permission");
        var forbidden = await readOnlyClient.PostAsJsonAsync("/api/v1/customers/42/emails", new { email = "nuevo@example.invalid", usageCodes = InvoiceUse }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var client = CreateClient("customers-write");
        var missingToken = await client.PostAsJsonAsync("/api/v1/customers/42/emails", new { email = "nuevo@example.invalid", usageCodes = InvoiceUse }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        var csrf = await GetCsrfAsync(client);
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, object body)
        {
            using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
            request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var created = await Send(HttpMethod.Post, "/api/v1/customers/42/emails", new { email = "nuevo@example.invalid", contact = "Contacto", usageCodes = InvoiceAndStatementsUses });
        var preserved = await Send(HttpMethod.Put, "/api/v1/customers/42/emails/8001", new { contact = "Otro contacto", usageCodes = InvoiceUse, email = (string?)null, expectedModifiedAt = "2025-01-01T00:00:00Z" });
        var replaced = await Send(HttpMethod.Put, "/api/v1/customers/42/emails/8001", new { email = "reemplazo@example.invalid", usageCodes = StatementsUse, expectedModifiedAt = "2025-01-01T00:00:00Z" });
        var activated = await Send(HttpMethod.Post, "/api/v1/customers/42/emails/8001/activate", new { expectedModifiedAt = "2025-01-01T00:00:00Z" });
        var deactivated = await Send(HttpMethod.Post, "/api/v1/customers/42/emails/8001/deactivate", new { expectedModifiedAt = "2025-01-01T00:00:00Z" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.NotNull(created.Headers.Location);
        Assert.Null(preserved.Headers.Location);
        Assert.Null(replaced.Headers.Location);
        Assert.Null(activated.Headers.Location);
        Assert.Null(deactivated.Headers.Location);
    }

    [Fact]
    public async Task EmailErrorsExposeOnlyControlledCodesWithoutSubmittedEmail()
    {
        using var client = CreateClient("customers-write");
        var csrf = await GetCsrfAsync(client);
        async Task<HttpResponseMessage> PostFor(int personId, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/customers/{personId}/emails") { Content = JsonContent.Create(body) };
            request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var duplicate = await PostFor(48, new { email = "sensitive@example.invalid", usageCodes = InvoiceUse });
        var unavailable = await PostFor(50, new { email = "sensitive@example.invalid", usageCodes = InvoiceUse });
        var invalid = await PostFor(42, new { email = "no-es-correo", usageCodes = Array.Empty<int>() });
        var unknownUse = await PostFor(42, new { email = "sensitive@example.invalid", usageCodes = UnknownUse });
        var duplicateUses = await PostFor(42, new { email = "sensitive@example.invalid", usageCodes = DuplicateUses });
        var inactiveUse = await PostFor(42, new { email = "sensitive@example.invalid", usageCodes = InactiveUse });
        var blacklisted = await PostFor(51, new { email = "sensitive@example.invalid", usageCodes = InvoiceUse });
        var missingPerson = await PostFor(999, new { email = "sensitive@example.invalid", usageCodes = InvoiceUse });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownUse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateUses.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inactiveUse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blacklisted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingPerson.StatusCode);
        foreach (var response in new[] { duplicate, unavailable, invalid, unknownUse, duplicateUses, inactiveUse, blacklisted, missingPerson })
        {
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("sensitive@example.invalid", body, StringComparison.Ordinal);
            Assert.DoesNotContain("MAI_FG_OMITIR_ENVIO", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("USR_CL_CVE", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task EmailStateAndUpdateConflictsAndMissingExpectedTimestampAreControlled()
    {
        using var client = CreateClient("customers-write");
        var csrf = await GetCsrfAsync(client);
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, object body)
        {
            using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
            request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var modified = await Send(HttpMethod.Put, "/api/v1/customers/49/emails/8001", new { email = (string?)null, usageCodes = InvoiceUse, expectedModifiedAt = "2025-01-01T00:00:00Z" });
        var duplicateUpdate = await Send(HttpMethod.Put, "/api/v1/customers/48/emails/8001", new { email = "duplicado@example.invalid", usageCodes = InvoiceUse, expectedModifiedAt = "2025-01-01T00:00:00Z" });
        var unavailableUpdate = await Send(HttpMethod.Put, "/api/v1/customers/50/emails/8001", new { email = "nuevo@example.invalid", usageCodes = InvoiceUse, expectedModifiedAt = "2025-01-01T00:00:00Z" });
        var invalidUpdate = await Send(HttpMethod.Put, "/api/v1/customers/42/emails/8001", new { email = "nuevo@example.invalid", usageCodes = Array.Empty<int>(), expectedModifiedAt = "2025-01-01T00:00:00Z" });
        var missingUpdate = await Send(HttpMethod.Put, "/api/v1/customers/999/emails/8001", new { email = (string?)null, usageCodes = InvoiceUse, expectedModifiedAt = "2025-01-01T00:00:00Z" });
        var duplicate = await Send(HttpMethod.Post, "/api/v1/customers/48/emails/8001/activate", new { expectedModifiedAt = "2025-01-01T00:00:00Z" });
        var missingTimestamp = await Send(HttpMethod.Post, "/api/v1/customers/42/emails/8001/deactivate", new { });
        var missingEmail = await Send(HttpMethod.Post, "/api/v1/customers/999/emails/8001/deactivate", new { expectedModifiedAt = "2025-01-01T00:00:00Z" });

        Assert.Equal(HttpStatusCode.Conflict, modified.StatusCode);
        Assert.Contains("email_modified", await modified.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, duplicateUpdate.StatusCode);
        Assert.Contains("email_duplicate", await duplicateUpdate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailableUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("email_duplicate", await duplicate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, missingTimestamp.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingEmail.StatusCode);
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
