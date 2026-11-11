using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CBL.IntegrationTests.Support;

public static class TestUsers
{
    public const string BorrowerEmail = "usuario@test.com";

    public const string AdminEmail = "admin@test.com";

    public const string SamplePassword = "123";

    public static readonly Guid BorrowerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid AdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");
}

public sealed record LoginUserResult(Guid Id, string Email, string FullName, string Role);

public sealed record LoginResult(string AccessToken, string TokenType, DateTime ExpiresAt, LoginUserResult User);

public static class AuthHelper
{
    public static async Task<LoginResult> LoginAsync(HttpClient client, string email, string password = TestUsers.SamplePassword)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LoginResult>())!;
    }

    /// <summary>Cliente HTTP con el token del usuario ya puesto (requiere el login de la historia 1).</summary>
    public static async Task<HttpClient> CreateAuthorizedClientAsync(
        this CblWebApplicationFactory factory,
        string email,
        string password = TestUsers.SamplePassword)
    {
        var client = factory.CreateClient();
        var login = await LoginAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        return client;
    }
}

/// <summary>Base de las pruebas de integración: restablece la base antes de cada prueba y serializa la colección.</summary>
[Collection(DatabaseCollection.Name)]
public abstract class IntegrationTestBase(DatabaseFixture fixture) : IAsyncLifetime
{
    protected DatabaseFixture Fixture { get; } = fixture;

    protected CblWebApplicationFactory Factory => Fixture.Factory;

    public virtual Task InitializeAsync() => Fixture.ResetAsync();

    public virtual Task DisposeAsync() => Task.CompletedTask;
}
