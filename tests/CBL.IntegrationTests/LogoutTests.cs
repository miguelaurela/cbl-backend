using System.Net;
using System.Net.Http.Headers;
using CBL.IntegrationTests.Support;

namespace CBL.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class LogoutTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [SqlServerFact]
    public async Task Logging_out_returns_204_and_the_same_token_is_rejected_right_away()
    {
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        (await client.GetAsync("/api/loans/mine")).StatusCode.Should().Be(HttpStatusCode.OK);

        var logout = await client.PostAsync("/api/auth/logout", content: null);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await client.GetAsync("/api/loans/mine");
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        after.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [SqlServerFact]
    public async Task Other_tokens_of_the_same_user_and_tokens_of_other_users_stay_valid()
    {
        var first = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var second = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);

        (await first.PostAsync("/api/auth/logout", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await first.GetAsync("/api/loans/mine")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await second.GetAsync("/api/loans/mine")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/loans")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SqlServerFact]
    public async Task A_new_login_after_logging_out_gets_a_working_session()
    {
        var old = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        await old.PostAsync("/api/auth/logout", content: null);

        var fresh = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        (await fresh.GetAsync("/api/loans/mine")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SqlServerFact]
    public async Task A_revoked_token_is_also_rejected_by_the_logout_endpoint()
    {
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        await client.PostAsync("/api/auth/logout", content: null);

        (await client.PostAsync("/api/auth/logout", content: null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task Logging_out_without_a_token_is_unauthenticated()
    {
        (await Factory.CreateClient().PostAsync("/api/auth/logout", content: null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task A_logout_does_not_block_the_login_endpoint_for_the_same_user()
    {
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);
        await client.PostAsync("/api/auth/logout", content: null);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-viejo-ya-revocado");
        var login = await AuthHelper.LoginAsync(Factory.CreateClient(), TestUsers.AdminEmail);

        login.AccessToken.Should().NotBeNullOrEmpty();
    }
}
