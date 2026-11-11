using System.Net;
using System.Net.Http.Json;
using CBL.IntegrationTests.Support;

namespace CBL.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class UsersTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<HttpClient> AdminAsync() => await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);

    private static Task<HttpResponseMessage> CreateAsync(HttpClient admin, string email, string password = "contraseña-larga", string role = "Borrower") =>
        admin.PostAsJsonAsync("/api/users", new { email, fullName = "Persona Nueva", password, role });

    [SqlServerFact]
    public async Task An_admin_creates_a_user_with_201_location_and_no_password_in_the_response()
    {
        var admin = await AdminAsync();

        var response = await CreateAsync(admin, "Nuevo@TEST.com");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().Should().EndWith($"/api/users/{id}");
        body.GetProperty("email").GetString().Should().Be("nuevo@test.com");
        body.GetProperty("role").GetString().Should().Be("Borrower");
        body.GetProperty("isActive").GetBoolean().Should().BeTrue();
        body.EnumerateObject().Select(p => p.Name).Should().NotContain(n => n.Contains("assword", StringComparison.OrdinalIgnoreCase));
    }

    [SqlServerFact]
    public async Task The_new_user_can_sign_in_with_the_password_the_admin_chose()
    {
        var admin = await AdminAsync();
        (await CreateAsync(admin, "login-nuevo@test.com", "contraseña-larga")).StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await AuthHelper.LoginAsync(Factory.CreateClient(), "login-nuevo@test.com", "contraseña-larga");

        login.User.Role.Should().Be("Borrower");
    }

    [SqlServerFact]
    public async Task A_seven_character_password_is_rejected_and_eight_is_accepted()
    {
        var admin = await AdminAsync();

        var short7 = await CreateAsync(admin, "siete@test.com", "1234567");
        var ok8 = await CreateAsync(admin, "ocho@test.com", "12345678");

        short7.StatusCode.Should().Be((HttpStatusCode)422);
        (await short7.ReadJsonAsync()).GetProperty("errors").TryGetProperty("password", out _).Should().BeTrue();
        ok8.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [SqlServerFact]
    public async Task A_duplicate_email_returns_409_even_with_other_capitalization()
    {
        var admin = await AdminAsync();

        var response = await CreateAsync(admin, "USUARIO@Test.com");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("email", out _).Should().BeTrue();
    }

    [SqlServerFact]
    public async Task Invalid_email_role_or_name_return_422_per_field()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/users", new { email = "no-es-correo", fullName = "", password = "contraseña-larga", role = "Usuario" });

        response.StatusCode.Should().Be((HttpStatusCode)422);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        errors.TryGetProperty("email", out _).Should().BeTrue();
        errors.TryGetProperty("fullName", out _).Should().BeTrue();
        errors.TryGetProperty("role", out _).Should().BeTrue();
    }

    [SqlServerFact]
    public async Task A_borrower_cannot_use_any_user_management_endpoint()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var someId = TestUsers.AdminId;

        (await borrower.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CreateAsync(borrower, "x@test.com")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await borrower.GetAsync($"/api/users/{someId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await borrower.PutAsJsonAsync($"/api/users/{someId}", new { fullName = "X", role = "Borrower", isActive = true })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task Without_a_token_user_management_is_unauthenticated()
    {
        (await Factory.CreateClient().GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task The_admin_lists_and_reads_users_and_unknown_ids_are_404()
    {
        var admin = await AdminAsync();

        var list = (await (await admin.GetAsync("/api/users")).ReadJsonAsync()).EnumerateArray().ToList();
        list.Select(u => u.GetProperty("email").GetString()).Should().Equal(TestUsers.AdminEmail, TestUsers.BorrowerEmail);

        var detail = await admin.GetAsync($"/api/users/{TestUsers.BorrowerId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detail.ReadJsonAsync()).GetProperty("email").GetString().Should().Be(TestUsers.BorrowerEmail);

        (await admin.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SqlServerFact]
    public async Task Updating_another_user_changes_name_role_and_can_reset_the_password()
    {
        var admin = await AdminAsync();

        var response = await admin.PutAsJsonAsync(
            $"/api/users/{TestUsers.BorrowerId}",
            new { fullName = "Nombre Cambiado", role = "Borrower", isActive = true, password = "nueva-contraseña" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("fullName").GetString().Should().Be("Nombre Cambiado");

        var client = Factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { email = TestUsers.BorrowerEmail, password = "123" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await AuthHelper.LoginAsync(client, TestUsers.BorrowerEmail, "nueva-contraseña")).User.FullName.Should().Be("Nombre Cambiado");
    }

    [SqlServerFact]
    public async Task A_deactivated_user_cannot_sign_in_and_their_loans_are_kept_and_reactivation_works()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var loanId = await borrower.CreateLoanAsync();
        var admin = await AdminAsync();

        var off = await admin.PutAsJsonAsync($"/api/users/{TestUsers.BorrowerId}", new { fullName = "Usuario de prueba", role = "Borrower", isActive = false });
        off.StatusCode.Should().Be(HttpStatusCode.OK);
        (await off.ReadJsonAsync()).GetProperty("isActive").GetBoolean().Should().BeFalse();

        var login = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = TestUsers.BorrowerEmail, password = "123" });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var loans = (await (await admin.GetAsync("/api/loans")).ReadJsonAsync()).EnumerateArray().ToList();
        loans.Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(loanId);

        (await admin.PutAsJsonAsync($"/api/users/{TestUsers.BorrowerId}", new { fullName = "Usuario de prueba", role = "Borrower", isActive = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await AuthHelper.LoginAsync(Factory.CreateClient(), TestUsers.BorrowerEmail)).User.Email.Should().Be(TestUsers.BorrowerEmail);
    }

    [SqlServerFact]
    public async Task An_admin_cannot_deactivate_themselves_or_change_their_own_role()
    {
        var admin = await AdminAsync();

        var deactivate = await admin.PutAsJsonAsync($"/api/users/{TestUsers.AdminId}", new { fullName = "Administrador de prueba", role = "Admin", isActive = false });
        var demote = await admin.PutAsJsonAsync($"/api/users/{TestUsers.AdminId}", new { fullName = "Administrador de prueba", role = "Borrower", isActive = true });
        var rename = await admin.PutAsJsonAsync($"/api/users/{TestUsers.AdminId}", new { fullName = "Otro nombre", role = "Admin", isActive = true });

        deactivate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deactivate.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        demote.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        rename.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SqlServerFact]
    public async Task Updating_an_unknown_user_returns_404_and_invalid_data_returns_422()
    {
        var admin = await AdminAsync();

        (await admin.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", new { fullName = "X", role = "Borrower", isActive = true })).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await admin.PutAsJsonAsync($"/api/users/{TestUsers.BorrowerId}", new { fullName = "", role = "Otro", isActive = true, password = "1234567" })).StatusCode
            .Should().Be((HttpStatusCode)422);
    }
}
