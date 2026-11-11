using FluentValidation.Results;

namespace CBL.Application.Common.Exceptions;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>Credenciales o sesión inválidas. El mensaje nunca revela si un correo existe.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);

/// <summary>Conflicto de estado, de unicidad o de concurrencia (HTTP 409).</summary>
public class ConflictException(string message, IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;
}

/// <summary>Dos escrituras compitieron por el mismo recurso y solo una prevalece.</summary>
public sealed class ConcurrencyConflictException(string message) : ConflictException(message);

/// <summary>Correo bloqueado temporalmente por intentos de inicio de sesión fallidos (HTTP 423).</summary>
public sealed class AccountLockedException(TimeSpan retryAfter)
    : Exception("Demasiados intentos fallidos. Inténtalo de nuevo más tarde.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

/// <summary>Datos de entrada inválidos; <see cref="Errors"/> trae los mensajes por campo en camelCase.</summary>
public sealed class ValidationFailedException : Exception
{
    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("Uno o más campos no son válidos.")
    {
        Errors = errors;
    }

    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [ToCamelCase(field)] = [message] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public static ValidationFailedException FromResult(ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new ValidationFailedException(errors);
    }

    public static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
