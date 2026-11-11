using CBL.Application.Abstractions;
using CBL.Application.Auth;
using CBL.Application.Auth.Login;
using CBL.Application.Common.Exceptions;
using CBL.Domain.Users;
using CBL.Infrastructure.Security;
using CBL.UnitTests.Support;

namespace CBL.UnitTests.Auth;

public class LoginHandlerTests
{
    private const string Password = "contraseña-correcta";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly FakeClock _clock = new();
    private readonly LoginAttemptTracker _tracker;
    private readonly LoginHandler _sut;

    public LoginHandlerTests()
    {
        _tracker = new LoginAttemptTracker(new AuthOptions(), _clock);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(1) == Password);
        _hasher.Hash(Arg.Any<string>()).Returns("hash-señuelo");
        _sut = new LoginHandler(_users, _hasher, _tokens, _tracker);
    }

    private User GivenUser(string email = "usuario@test.com", Role role = Role.Borrower, bool active = true)
    {
        var user = Builders.NewUser(role, email, active);
        _users.GetByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(user);
        _tokens.Issue(user).Returns(new IssuedToken("jwt-de-prueba", _clock.UtcNow.AddMinutes(10), "jti-1"));
        return user;
    }

    [Fact]
    public async Task Valid_credentials_return_token_expiry_and_user()
    {
        var user = GivenUser(role: Role.Admin);

        var response = await _sut.HandleAsync(new LoginRequest("usuario@test.com", Password), default);

        response.AccessToken.Should().Be("jwt-de-prueba");
        response.TokenType.Should().Be("Bearer");
        response.ExpiresAt.Should().Be(_clock.UtcNow.AddMinutes(10));
        response.User.Should().BeEquivalentTo(new LoginUserDto(user.Id, user.Email, user.FullName, "Admin"));
    }

    [Fact]
    public async Task Unknown_email_wrong_password_and_deactivated_user_fail_with_the_same_generic_error()
    {
        GivenUser("existente@test.com");
        GivenUser("inactivo@test.com", active: false);

        var unknown = await Record.ExceptionAsync(() => _sut.HandleAsync(new LoginRequest("nadie@test.com", Password), default));
        var wrongPassword = await Record.ExceptionAsync(() => _sut.HandleAsync(new LoginRequest("existente@test.com", "mal"), default));
        var deactivated = await Record.ExceptionAsync(() => _sut.HandleAsync(new LoginRequest("inactivo@test.com", Password), default));

        unknown.Should().BeOfType<UnauthorizedException>();
        wrongPassword.Should().BeOfType<UnauthorizedException>();
        deactivated.Should().BeOfType<UnauthorizedException>();
        unknown!.Message.Should().Be(wrongPassword!.Message).And.Be(deactivated!.Message);
    }

    [Fact]
    public async Task Unknown_email_still_pays_the_hashing_cost_to_avoid_timing_enumeration()
    {
        await Record.ExceptionAsync(() => _sut.HandleAsync(new LoginRequest("nadie@test.com", Password), default));

        _hasher.Received(1).Hash(Password);
    }

    [Theory]
    [InlineData("existente@test.com")]
    [InlineData("nadie@test.com")]
    public async Task Sixth_attempt_with_the_same_email_is_locked_whether_the_email_exists_or_not(string email)
    {
        GivenUser("existente@test.com");

        for (var i = 0; i < 5; i++)
        {
            (await Record.ExceptionAsync(() => _sut.HandleAsync(new LoginRequest(email, "mal"), default)))
                .Should().BeOfType<UnauthorizedException>();
        }

        var sixth = await Record.ExceptionAsync(() => _sut.HandleAsync(new LoginRequest(email, "mal"), default));

        sixth.Should().BeOfType<AccountLockedException>()
            .Which.RetryAfter.Should().BeCloseTo(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task A_locked_email_stays_locked_even_with_the_right_password_until_the_window_ends()
    {
        GivenUser("existente@test.com");

        for (var i = 0; i < 5; i++)
        {
            await Record.ExceptionAsync(() => _sut.HandleAsync(new LoginRequest("existente@test.com", "mal"), default));
        }

        (await Record.ExceptionAsync(() => _sut.HandleAsync(new LoginRequest("existente@test.com", Password), default)))
            .Should().BeOfType<AccountLockedException>();

        _clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        var response = await _sut.HandleAsync(new LoginRequest("existente@test.com", Password), default);
        response.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Successful_login_clears_previous_failures()
    {
        GivenUser("existente@test.com");

        for (var i = 0; i < 4; i++)
        {
            await Record.ExceptionAsync(() => _sut.HandleAsync(new LoginRequest("existente@test.com", "mal"), default));
        }

        await _sut.HandleAsync(new LoginRequest("existente@test.com", Password), default);

        for (var i = 0; i < 4; i++)
        {
            (await Record.ExceptionAsync(() => _sut.HandleAsync(new LoginRequest("existente@test.com", "mal"), default)))
                .Should().BeOfType<UnauthorizedException>();
        }
    }

    [Fact]
    public async Task Email_is_normalized_so_other_capitalization_signs_in_as_the_same_user()
    {
        GivenUser("usuario@test.com");

        var response = await _sut.HandleAsync(new LoginRequest("  USUARIO@Test.COM ", Password), default);

        response.AccessToken.Should().Be("jwt-de-prueba");
        await _users.Received(1).GetByEmailAsync("usuario@test.com", Arg.Any<CancellationToken>());
    }
}
