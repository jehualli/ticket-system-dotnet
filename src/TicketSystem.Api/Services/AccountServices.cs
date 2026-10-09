using Jehualli.TicketSystem.Api.Auth;
using Jehualli.TicketSystem.Api.Data;
using Jehualli.TicketSystem.Api.Dtos;
using Jehualli.TicketSystem.Api.Errors;
using Jehualli.TicketSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Jehualli.TicketSystem.Api.Services;

public class AuthService(AppDbContext db, JwtTokenService tokens)
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        // Same error for "no such user" and "wrong password", so the API doesn't reveal which emails exist.
        if (user is null || !user.Active || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        return new AuthResponse(tokens.CreateToken(user), "Bearer", user.Id, user.Name, user.Email, user.Role);
    }

    public async Task<UserResponse> MeAsync(CurrentUser user, CancellationToken ct = default) =>
        (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id, ct)
         ?? throw new NotFoundException("User", user.Id)).ToResponse();
}

public class UserService(AppDbContext db)
{
    public async Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken ct = default) =>
        (await db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct)).Select(u => u.ToResponse()).ToList();

    public async Task<UserResponse> GetAsync(Guid id, CancellationToken ct = default) =>
        (await FindAsync(id, ct)).ToResponse();

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email, ct))
            throw new ConflictException($"A user with email {request.Email} already exists");

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role!.Value,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.ToResponse();
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        if (request.Name is not null) user.Name = request.Name;
        if (request.Role is { } role) user.Role = role;
        if (request.Active is { } active) user.Active = active;
        await db.SaveChangesAsync(ct);
        return user.ToResponse();
    }

    private async Task<User> FindAsync(Guid id, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User", id);
}

public class ClientService(AppDbContext db)
{
    public async Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken ct = default) =>
        (await db.Clients.AsNoTracking().OrderBy(c => c.CompanyName).ToListAsync(ct)).Select(c => c.ToResponse()).ToList();

    public async Task<ClientResponse> GetAsync(Guid id, CancellationToken ct = default) =>
        (await FindAsync(id, ct)).ToResponse();

    public async Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
    {
        if (await db.Clients.AnyAsync(c => c.Email == request.Email, ct))
            throw new ConflictException($"A client with email {request.Email} already exists");

        if (request.UserId is { } userId)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
            if (user.Role != UserRole.Client)
                throw new BadRequestException("Only a CLIENT user can be linked to a client");
            if (await db.Clients.AnyAsync(c => c.UserId == userId, ct))
                throw new ConflictException("That user is already linked to a client");
        }

        var client = new Client
        {
            CompanyName = request.CompanyName,
            ContactName = request.ContactName,
            Email = request.Email,
            Phone = request.Phone,
            UserId = request.UserId,
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);
        return client.ToResponse();
    }

    public async Task<ClientResponse> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default)
    {
        var client = await FindAsync(id, ct);
        if (request.CompanyName is not null) client.CompanyName = request.CompanyName;
        if (request.ContactName is not null) client.ContactName = request.ContactName;
        if (request.Phone is not null) client.Phone = request.Phone;
        if (request.Active is { } active) client.Active = active;
        await db.SaveChangesAsync(ct);
        return client.ToResponse();
    }

    private async Task<Client> FindAsync(Guid id, CancellationToken ct) =>
        await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Client", id);
}
