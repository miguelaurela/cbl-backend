using CBL.Application.Abstractions;
using CBL.Application.Auth.Logout;
using CBL.Application.Common.Exceptions;
using CBL.Infrastructure.Security;
using CBL.UnitTests.Support;

namespace CBL.UnitTests.Auth;

public class LogoutTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void A_revoked_token_is_reported_as_revoked_and_others_are_not()
    {
        var store = new TokenRevocationStore(_clock);

        store.Revoke("jti-1", _clock.UtcNow.AddMinutes(10));

        store.IsRevoked("jti-1").Should().BeTrue();
        store.IsRevoked("jti-2").Should().BeFalse();
    }

    [Fact]
    public void The_entry_outlives_the_token_by_the_clock_skew_and_then_disappears()
    {
        var store = new TokenRevocationStore(_clock);
        var expiresAt = _clock.UtcNow.AddMinutes(10);
        store.Revoke("jti-1", expiresAt);

        _clock.UtcNow = expiresAt + TimeSpan.FromSeconds(30);
        store.IsRevoked("jti-1").Should().BeTrue("JwtBearer todavía aceptaría el token hasta exp + 30 s");

        _clock.UtcNow = expiresAt + TokenRevocationStore.Retention + TimeSpan.FromSeconds(1);
        store.IsRevoked("jti-1").Should().BeFalse();
        store.Count.Should().Be(0);
    }

    [Fact]
    public void Expired_entries_are_purged_so_the_store_does_not_grow_without_limit()
    {
        var store = new TokenRevocationStore(_clock);

        for (var i = 0; i < 50; i++)
        {
            store.Revoke($"viejo-{i}", _clock.UtcNow.AddMinutes(10));
        }

        _clock.Advance(TimeSpan.FromMinutes(30));
        store.Revoke("nuevo", _clock.UtcNow.AddMinutes(10));

        store.Count.Should().Be(1);
    }

    [Fact]
    public void Logging_out_revokes_the_current_token_until_its_expiration()
    {
        var store = Substitute.For<ITokenRevocationStore>();
        var user = Substitute.For<ICurrentUser>();
        var expiresAt = _clock.UtcNow.AddMinutes(7);
        user.TokenId.Returns("jti-actual");
        user.TokenExpiresAtUtc.Returns(expiresAt);

        new LogoutHandler(user, store, _clock).Handle();

        store.Received(1).Revoke("jti-actual", expiresAt);
    }

    [Fact]
    public void Without_a_token_id_there_is_no_session_to_close()
    {
        var store = Substitute.For<ITokenRevocationStore>();
        var user = Substitute.For<ICurrentUser>();
        user.TokenId.Returns((string?)null);

        var act = () => new LogoutHandler(user, store, _clock).Handle();

        act.Should().Throw<UnauthorizedException>();
        store.DidNotReceiveWithAnyArgs().Revoke(default!, default);
    }
}
