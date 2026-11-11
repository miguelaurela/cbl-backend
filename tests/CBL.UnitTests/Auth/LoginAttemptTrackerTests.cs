using CBL.Application.Auth;
using CBL.Infrastructure.Security;
using CBL.UnitTests.Support;

namespace CBL.UnitTests.Auth;

public class LoginAttemptTrackerTests
{
    private readonly FakeClock _clock = new();

    private LoginAttemptTracker Create(int maxFailed = 5, int lockoutMinutes = 15, int maxEntries = 10_000) =>
        new(new AuthOptions { MaxFailedAttempts = maxFailed, LockoutMinutes = lockoutMinutes }, _clock, maxEntries);

    [Fact]
    public void Five_failures_in_the_window_lock_the_email_for_fifteen_minutes()
    {
        var sut = Create();

        for (var i = 0; i < 4; i++)
        {
            sut.RegisterFailure("a@test.com");
        }

        sut.IsLocked("a@test.com", out _).Should().BeFalse("4 fallos todavía no bloquean");

        sut.RegisterFailure("a@test.com");

        sut.IsLocked("a@test.com", out var retryAfter).Should().BeTrue();
        retryAfter.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void The_lock_expires_and_the_counter_starts_over()
    {
        var sut = Create();
        for (var i = 0; i < 5; i++)
        {
            sut.RegisterFailure("a@test.com");
        }

        _clock.Advance(TimeSpan.FromMinutes(15));

        sut.IsLocked("a@test.com", out _).Should().BeFalse();

        for (var i = 0; i < 4; i++)
        {
            sut.RegisterFailure("a@test.com");
        }

        sut.IsLocked("a@test.com", out _).Should().BeFalse("el contador empezó de nuevo");
    }

    [Fact]
    public void Failures_older_than_the_window_do_not_add_up()
    {
        var sut = Create();
        for (var i = 0; i < 4; i++)
        {
            sut.RegisterFailure("a@test.com");
        }

        _clock.Advance(TimeSpan.FromMinutes(16));
        sut.RegisterFailure("a@test.com");

        sut.IsLocked("a@test.com", out _).Should().BeFalse();
    }

    [Fact]
    public void Email_keys_ignore_capitalization()
    {
        var sut = Create(maxFailed: 2);
        sut.RegisterFailure("Usuario@Test.com");
        sut.RegisterFailure("usuario@test.COM");

        sut.IsLocked("USUARIO@TEST.COM", out _).Should().BeTrue();
    }

    [Fact]
    public void Limits_are_configurable()
    {
        var sut = Create(maxFailed: 2, lockoutMinutes: 1);
        sut.RegisterFailure("a@test.com");
        sut.RegisterFailure("a@test.com");

        sut.IsLocked("a@test.com", out var retryAfter).Should().BeTrue();
        retryAfter.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Reset_clears_the_counter_and_the_lock()
    {
        var sut = Create(maxFailed: 2);
        sut.RegisterFailure("a@test.com");
        sut.RegisterFailure("a@test.com");

        sut.Reset("a@test.com");

        sut.IsLocked("a@test.com", out _).Should().BeFalse();
    }

    [Fact]
    public void Memory_is_bounded_even_if_arbitrary_emails_keep_failing()
    {
        var sut = Create(maxEntries: 20);

        for (var i = 0; i < 500; i++)
        {
            sut.RegisterFailure($"correo-{i}@test.com");
        }

        sut.Count.Should().BeLessThanOrEqualTo(20);
    }

    [Fact]
    public void Expired_entries_are_purged_to_make_room()
    {
        var sut = Create(maxEntries: 5);
        for (var i = 0; i < 5; i++)
        {
            sut.RegisterFailure($"viejo-{i}@test.com");
        }

        _clock.Advance(TimeSpan.FromMinutes(20));
        sut.RegisterFailure("nuevo@test.com");

        sut.Count.Should().Be(1);
    }
}
