using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CBL.Infrastructure.Persistence;

/// <summary>Un único <c>SaveChangesAsync</c> por operación: EF lo ejecuta como una transacción implícita.</summary>
internal sealed class UnitOfWork(CblDbContext db) : IUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "El recurso fue modificado por otra operación; vuelve a consultarlo e inténtalo de nuevo.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            throw new ConflictException("Ya existe un registro con esos datos.");
        }
    }
}
