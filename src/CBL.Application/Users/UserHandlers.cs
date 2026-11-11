using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Domain.Users;

namespace CBL.Application.Users;

/// <summary>Alta de usuario (solo Admin). Correo único y sin distinguir mayúsculas; contraseña con hash (FR-020, FR-021).</summary>
public sealed class CreateUserHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<UserDto> HandleAsync(CreateUserRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);

        if (await users.EmailExistsAsync(email, ct))
        {
            throw new ConflictException(
                "Ya existe un usuario con ese correo.",
                new Dictionary<string, string[]> { ["email"] = ["Ya existe un usuario con ese correo."] });
        }

        var user = User.Create(
            email,
            request.FullName,
            hasher.Hash(request.Password),
            Enum.Parse<Role>(request.Role),
            clock.UtcNow);

        await users.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return user.ToDto();
    }
}

public sealed class GetUsersHandler(IUserRepository users)
{
    public async Task<IReadOnlyList<UserDto>> HandleAsync(CancellationToken ct) =>
        (await users.ListAsync(ct)).Select(u => u.ToDto()).ToList();
}

public sealed class GetUserByIdHandler(IUserRepository users)
{
    public async Task<UserDto> HandleAsync(Guid id, CancellationToken ct) =>
        (await users.GetByIdAsync(id, ct) ?? throw new NotFoundException("El usuario no existe.")).ToDto();
}

/// <summary>
/// Actualiza o desactiva un usuario (solo Admin). Un Admin NO puede desactivar su propia cuenta ni cambiarse su propio
/// rol (para no dejar el sistema sin administradores): se rechaza con 403. La desactivación es lógica: el historial se
/// conserva. Un Admin puede reactivar usuarios (FR-020, FR-022).
/// </summary>
public sealed class UpdateUserHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task<UserDto> HandleAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct) ?? throw new NotFoundException("El usuario no existe.");
        var newRole = Enum.Parse<Role>(request.Role);

        if (user.Id == currentUser.UserId)
        {
            if (!request.IsActive)
            {
                throw new ForbiddenException("No puedes desactivar tu propia cuenta.");
            }

            if (newRole != user.Role)
            {
                throw new ForbiddenException("No puedes cambiar tu propio rol.");
            }
        }

        user.Update(request.FullName, newRole);

        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        if (!string.IsNullOrEmpty(request.Password))
        {
            user.SetPasswordHash(hasher.Hash(request.Password));
        }

        await unitOfWork.SaveChangesAsync(ct);

        return user.ToDto();
    }
}
