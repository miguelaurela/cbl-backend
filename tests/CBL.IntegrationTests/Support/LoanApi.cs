using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CBL.Application.Abstractions;
using CBL.Domain.Loans;
using CBL.Domain.Users;
using CBL.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CBL.IntegrationTests.Support;

/// <summary>Ayudantes para las pruebas de préstamos: llamadas a la API y acceso directo a la base para verificar.</summary>
public static class LoanApi
{
    public static Task<HttpResponseMessage> RequestLoanAsync(this HttpClient client, decimal amount, int termMonths) =>
        client.PostAsJsonAsync("/api/loans", new { amount, termMonths });

    /// <summary>Solicita un préstamo válido y devuelve su id (comprueba el 201).</summary>
    public static async Task<Guid> CreateLoanAsync(this HttpClient client, decimal amount = 5000m, int termMonths = 12)
    {
        var response = await client.RequestLoanAsync(amount, termMonths);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> ApproveAsync(this HttpClient client, Guid id, object? body = null) =>
        client.PostAsJsonAsync($"/api/loans/{id}/approve", body);

    public static Task<HttpResponseMessage> RejectAsync(this HttpClient client, Guid id, object? body = null) =>
        client.PostAsJsonAsync($"/api/loans/{id}/reject", body);

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Inserta un segundo prestamista directamente en la base (la gestión de usuarios llega en la historia 6).</summary>
    public static async Task<(Guid Id, string Email)> AddBorrowerAsync(this CblWebApplicationFactory factory, string? email = null)
    {
        email ??= $"prestamista-{Guid.NewGuid():N}@test.com";

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CblDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = User.Create(email, "Otro prestamista", hasher.Hash(TestUsers.SamplePassword), Role.Borrower, clock.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user.Id, user.Email);
    }

    public static async Task<List<LoanDecisionAudit>> AuditRowsAsync(this CblWebApplicationFactory factory, Guid loanId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CblDbContext>();

        return await db.LoanDecisionAudits.AsNoTracking().Where(a => a.LoanId == loanId).ToListAsync();
    }

    public static async Task<LoanStatus> StoredStatusAsync(this CblWebApplicationFactory factory, Guid loanId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CblDbContext>();

        return (await db.Loans.AsNoTracking().SingleAsync(l => l.Id == loanId)).Status;
    }
}
