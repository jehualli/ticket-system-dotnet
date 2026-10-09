using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jehualli.TicketSystem.Api.Data;
using Jehualli.TicketSystem.Api.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jehualli.TicketSystem.Api.Tests.Integration;

// Boots the real API in memory against a throwaway SQL Server database.
// Set TEST_SQLSERVER to a connection string (no Database=) to run these; otherwise they are skipped.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly string? ServerConnection = Environment.GetEnvironmentVariable("TEST_SQLSERVER");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    private readonly string _database = $"TicketSystemTests_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection = new SqlConnectionStringBuilder(ServerConnection ?? "") { InitialCatalog = _database };

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connection.ConnectionString);
        builder.UseSetting("Jwt:Key", "integration-test-key-0123456789abcdef");
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (ServerConnection is not null)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
    }

    public async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }
}
