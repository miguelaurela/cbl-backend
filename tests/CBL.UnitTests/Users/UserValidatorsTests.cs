using CBL.Application.Users;

namespace CBL.UnitTests.Users;

public class UserValidatorsTests
{
    private readonly CreateUserRequestValidator _create = new();
    private readonly UpdateUserRequestValidator _update = new();

    private static CreateUserRequest Valid(string? email = null, string? name = null, string? password = null, string? role = null) =>
        new(email ?? "persona@test.com", name ?? "Persona", password ?? "12345678", role ?? "Borrower");

    [Fact]
    public void A_valid_creation_passes() => _create.Validate(Valid()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    [InlineData("", false)]
    public void The_password_needs_at_least_eight_characters(string password, bool valid)
    {
        var result = _create.Validate(Valid(password: password));

        result.IsValid.Should().Be(valid);
        if (!valid)
        {
            result.Errors.Should().OnlyContain(e => e.PropertyName == "Password");
        }
    }

    [Theory]
    [InlineData("no-es-un-correo")]
    [InlineData("")]
    [InlineData("@test.com")]
    public void The_email_must_have_a_valid_format(string email) =>
        _create.Validate(Valid(email: email)).Errors.Should().Contain(e => e.PropertyName == "Email");

    [Fact]
    public void The_email_has_a_maximum_length()
    {
        var email = new string('a', 250) + "@test.com";

        _create.Validate(Valid(email: email)).Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Borrower", true)]
    [InlineData("admin", false)]
    [InlineData("Usuario", false)]
    [InlineData("", false)]
    public void The_role_must_be_exactly_admin_or_borrower(string role, bool valid) =>
        _create.Validate(Valid(role: role)).IsValid.Should().Be(valid);

    [Fact]
    public void The_name_is_required_and_bounded()
    {
        _create.Validate(Valid(name: "")).Errors.Should().Contain(e => e.PropertyName == "FullName");
        _create.Validate(Valid(name: new string('x', 201))).Errors.Should().Contain(e => e.PropertyName == "FullName");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    public void On_update_the_password_is_optional_but_follows_the_same_policy_when_present(string? password, bool valid)
    {
        var result = _update.Validate(new UpdateUserRequest("Persona", "Borrower", true, password));

        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public void On_update_an_invalid_role_or_empty_name_is_rejected()
    {
        var result = _update.Validate(new UpdateUserRequest("", "Otro", true));

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo("FullName", "Role");
    }
}
