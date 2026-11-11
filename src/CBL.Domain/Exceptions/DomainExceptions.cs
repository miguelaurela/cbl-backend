namespace CBL.Domain.Exceptions;

/// <summary>Violación de una regla de negocio del dominio.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Transición de estado no permitida (por ejemplo, decidir un préstamo ya decidido).</summary>
public sealed class InvalidLoanTransitionException(string message) : DomainException(message);

/// <summary>Se intentó editar o eliminar un préstamo que ya no está Pendiente.</summary>
public sealed class LoanNotModifiableException(string message) : DomainException(message);

/// <summary>Un dato no cumple una invariante del dominio; indica el campo afectado.</summary>
public sealed class DomainValidationException(string field, string message) : DomainException(message)
{
    public string Field { get; } = field;
}
