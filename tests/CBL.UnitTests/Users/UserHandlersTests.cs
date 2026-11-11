using CBL.Application.Common.Exceptions;
using CBL.Application.Users;
using CBL.Domain.Users;
using CBL.Infrastructure.Persistence;
using CBL.UnitTests.Support;
using Microsoft.EntityFrameworkCore;

namespace CBL.UnitTests.Users;

public class UserHandlersTests
{
    private static async Task<TestApp> AdminAppAsync()
    {
        var app = await TestApp.CreateAsync();
        app.SignInAs(SeedData.AdminId, Role.Admin);

        return app;
    }

    [Fact]
    public async Task Creating_a_user_normalizes_the_email_hashes_the_password_and_never_returns_it()
    {
        using var app = await AdminAppAsync();

        var dto = await app.CreateUser.HandleAsync(
            new CreateUserRequest("  Nuevo@TEST.com ", "Persona Nueva", "contraseña-larga", "Borrower"), default);

        dto.Email.Should().Be("nuevo@test.com");
        dto.Role.Should().Be("Borrower");
        dto.IsActive.Should().BeTrue();

        var stored = await app.Db.Users.SingleAsync(u => u.Email == "nuevo@test.com");
        stored.PasswordHash.Should().NotBe("contraseña-larga");
        app.Hasher.Verify(stored.PasswordHash, "contraseña-larga").Should().BeTrue();
        typeof(UserDto).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("Password"));
    }

    [Fact]
    public async Task A_duplicate_email_conflicts_even_with_other_capitalization_and_names_the_email_field()
    {
        using var app = await AdminAppAsync();

        var act = () => app.CreateUser.HandleAsync(
            new CreateUserRequest("USUARIO@test.com", "Duplicado", "contraseña-larga", "Borrower"), default);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Errors.Should().ContainKey("email");
        (await app.Db.Users.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task An_admin_updates_another_user_name_role_and_password()
    {
        using var app = await AdminAppAsync();

        var dto = await app.UpdateUser.HandleAsync(
            SeedData.BorrowerId, new UpdateUserRequest("Nombre Nuevo", "Admin", true, "otra-contraseña"), default);

        dto.FullName.Should().Be("Nombre Nuevo");
        dto.Role.Should().Be("Admin");
        var stored = await app.Db.Users.SingleAsync(u => u.Id == SeedData.BorrowerId);
        app.Hasher.Verify(stored.PasswordHash, "otra-contraseña").Should().BeTrue();
        app.Hasher.Verify(stored.PasswordHash, "123").Should().BeFalse();
    }

    [Fact]
    public async Task Without_a_password_in_the_request_the_current_password_is_kept()
    {
        using var app = await AdminAppAsync();

        await app.UpdateUser.HandleAsync(SeedData.BorrowerId, new UpdateUserRequest("Solo Nombre", "Borrower", true), default);

        var stored = await app.Db.Users.SingleAsync(u => u.Id == SeedData.BorrowerId);
        app.Hasher.Verify(stored.PasswordHash, "123").Should().BeTrue();
    }

    [Fact]
    public async Task Deactivation_is_logical_and_reactivation_is_allowed()
    {
        using var app = await AdminAppAsync();

        var off = await app.UpdateUser.HandleAsync(SeedData.BorrowerId, new UpdateUserRequest("Usuario", "Borrower", false), default);
        off.IsActive.Should().BeFalse();
        (await app.Db.Users.CountAsync()).Should().Be(2, "nunca se elimina un usuario");

        var on = await app.UpdateUser.HandleAsync(SeedData.BorrowerId, new UpdateUserRequest("Usuario", "Borrower", true), default);
        on.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_their_own_account()
    {
        using var app = await AdminAppAsync();

        var act = () => app.UpdateUser.HandleAsync(SeedData.AdminId, new UpdateUserRequest("Administrador", "Admin", false), default);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await app.Db.Users.SingleAsync(u => u.Id == SeedData.AdminId)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task An_admin_cannot_change_their_own_role()
    {
        using var app = await AdminAppAsync();

        var act = () => app.UpdateUser.HandleAsync(SeedData.AdminId, new UpdateUserRequest("Administrador", "Borrower", true), default);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await app.Db.Users.SingleAsync(u => u.Id == SeedData.AdminId)).Role.Should().Be(Role.Admin);
    }

    [Fact]
    public async Task An_admin_can_still_edit_their_own_name_keeping_role_and_active()
    {
        using var app = await AdminAppAsync();

        var dto = await app.UpdateUser.HandleAsync(SeedData.AdminId, new UpdateUserRequest("Nuevo Nombre Admin", "Admin", true), default);

        dto.FullName.Should().Be("Nuevo Nombre Admin");
    }

    [Fact]
    public async Task Unknown_users_are_not_found_for_get_and_update()
    {
        using var app = await AdminAppAsync();

        var get = () => app.GetUserById.HandleAsync(Guid.NewGuid(), default);
        var update = () => app.UpdateUser.HandleAsync(Guid.NewGuid(), new UpdateUserRequest("X", "Borrower", true), default);

        await get.Should().ThrowAsync<NotFoundException>();
        await update.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Listing_returns_every_user_ordered_by_email()
    {
        using var app = await AdminAppAsync();
        await app.AddUserAsync(Role.Borrower, "a-primero@test.com");

        var users = await app.GetUsers.HandleAsync(default);

        users.Select(u => u.Email).Should().Equal("a-primero@test.com", "admin@test.com", "usuario@test.com");
    }
}
