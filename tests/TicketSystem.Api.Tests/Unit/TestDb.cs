using Jehualli.TicketSystem.Api.Auth;
using Jehualli.TicketSystem.Api.Data;
using Jehualli.TicketSystem.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Jehualli.TicketSystem.Api.Tests.Unit;

// A fresh in-memory SQLite database per test, with one user of each role and two clients.
// SQLite is a real relational database, so foreign keys and LINQ translation behave like SQL Server.
public sealed class TestDb : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public AppDbContext Db { get; }

    public CurrentUser Admin { get; private set; } = null!;
    public CurrentUser Agent { get; private set; } = null!;
    public CurrentUser Developer { get; private set; } = null!;
    public CurrentUser OtherDeveloper { get; private set; } = null!;
    public CurrentUser Alice { get; private set; } = null!;
    public CurrentUser Bob { get; private set; } = null!;
    public Guid AcmeId { get; private set; }
    public Guid TechStartId { get; private set; }

    private TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
    }

    public static async Task<TestDb> CreateAsync()
    {
        var t = new TestDb();
        await t.Db.Database.EnsureCreatedAsync();

        t.Admin = await t.AddUserAsync("admin@test", UserRole.Admin);
        t.Agent = await t.AddUserAsync("agent@test", UserRole.Agent);
        t.Developer = await t.AddUserAsync("dev1@test", UserRole.Developer);
        t.OtherDeveloper = await t.AddUserAsync("dev2@test", UserRole.Developer);
        t.Alice = await t.AddUserAsync("alice@acme.test", UserRole.Client);
        t.Bob = await t.AddUserAsync("bob@techstart.test", UserRole.Client);

        t.AcmeId = await t.AddClientAsync("Acme", t.Alice);
        t.TechStartId = await t.AddClientAsync("TechStart", t.Bob);
        return t;
    }

    private async Task<CurrentUser> AddUserAsync(string email, UserRole role)
    {
        var user = new User { Name = email, Email = email, PasswordHash = "x", Role = role };
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        return new CurrentUser(user.Id, role);
    }

    private async Task<Guid> AddClientAsync(string company, CurrentUser contact)
    {
        var client = new Client { CompanyName = company, ContactName = company, Email = $"{company}@test", UserId = contact.Id };
        Db.Clients.Add(client);
        await Db.SaveChangesAsync();
        return client.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
