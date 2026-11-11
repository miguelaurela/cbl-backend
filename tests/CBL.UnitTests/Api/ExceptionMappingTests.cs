using System.Text.Json;
using CBL.Api.Middleware;
using CBL.Application.Common.Exceptions;
using CBL.Domain.Exceptions;

namespace CBL.UnitTests.Api;

public class ExceptionMappingTests
{
    public static TheoryData<Exception, int> Cases => new()
    {
        { new ValidationFailedException("amount", "inválido"), 422 },
        { new DomainValidationException("termMonths", "inválido"), 422 },
        { new UnauthorizedException("no"), 401 },
        { new ForbiddenException("no"), 403 },
        { new NotFoundException("no"), 404 },
        { new ConflictException("dup"), 409 },
        { new ConcurrencyConflictException("carrera"), 409 },
        { new InvalidLoanTransitionException("ya decidido"), 409 },
        { new LoanNotModifiableException("ya decidido"), 409 },
        { new AccountLockedException(TimeSpan.FromMinutes(15)), 423 },
        { new JsonException("json roto"), 400 },
        { new InvalidOperationException("detalle interno"), 500 }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Maps_each_exception_to_the_documented_status(Exception exception, int expectedStatus) =>
        ApiExceptionHandler.Map(exception).Status.Should().Be(expectedStatus);

    [Fact]
    public void Validation_errors_travel_per_field_in_camel_case()
    {
        var mapping = ApiExceptionHandler.Map(new DomainValidationException("TermMonths", "plazo inválido"));

        mapping.Errors.Should().ContainKey("termMonths").WhoseValue.Should().Equal("plazo inválido");
    }

    [Fact]
    public void Locked_account_sends_retry_after_in_seconds()
    {
        var mapping = ApiExceptionHandler.Map(new AccountLockedException(TimeSpan.FromMinutes(15)));

        mapping.RetryAfterSeconds.Should().Be(900);
    }

    [Fact]
    public void Conflict_keeps_field_errors_when_provided()
    {
        var errors = new Dictionary<string, string[]> { ["email"] = ["Ya existe un usuario con ese correo."] };

        ApiExceptionHandler.Map(new ConflictException("dup", errors)).Errors.Should().ContainKey("email");
    }

    [Fact]
    public void Unexpected_errors_never_leak_internal_details()
    {
        var mapping = ApiExceptionHandler.Map(new InvalidOperationException("cadena de conexión: Server=secreto"));

        mapping.Status.Should().Be(500);
        $"{mapping.Title} {mapping.Detail}".Should().NotContain("secreto");
    }
}
