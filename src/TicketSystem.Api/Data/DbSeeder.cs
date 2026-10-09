using Jehualli.TicketSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Jehualli.TicketSystem.Api.Data;

// Creates the demo accounts listed in the README. Safe to run on every startup.
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        await SeedUserAsync(db, "System Admin", "admin@company.com", "Admin@123", UserRole.Admin);
        await SeedUserAsync(db, "Support Agent", "agent@company.com", "Agent@123", UserRole.Agent);
        await SeedUserAsync(db, "John Developer", "dev1@company.com", "Dev@123", UserRole.Developer);
        await SeedUserAsync(db, "Jane Developer", "dev2@company.com", "Dev@123", UserRole.Developer);
        var alice = await SeedUserAsync(db, "Alice Smith", "client1@acme.com", "Client@123", UserRole.Client);
        var bob = await SeedUserAsync(db, "Bob Johnson", "client2@techstart.io", "Client@123", UserRole.Client);

        await SeedClientAsync(db, "Acme Corporation", "Alice Smith", "client1@acme.com", "+1-555-0101", alice);
        await SeedClientAsync(db, "TechStart Inc.", "Bob Johnson", "client2@techstart.io", "+1-555-0202", bob);

        logger.LogInformation("Seed data ready");
    }

    private static async Task<User> SeedUserAsync(
        AppDbContext db, string name, string email, string password, UserRole role)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existing is not null) return existing;

        var user = new User
        {
            Name = name,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedClientAsync(
        AppDbContext db, string company, string contact, string email, string phone, User user)
    {
        if (await db.Clients.AnyAsync(c => c.UserId == user.Id)) return;

        db.Clients.Add(new Client
        {
            CompanyName = company,
            ContactName = contact,
            Email = email,
            Phone = phone,
            UserId = user.Id,
        });
        await db.SaveChangesAsync();
    }
}
