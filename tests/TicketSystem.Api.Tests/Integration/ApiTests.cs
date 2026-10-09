using System.Net;
using System.Net.Http.Json;
using Jehualli.TicketSystem.Api.Dtos;
using Jehualli.TicketSystem.Api.Errors;
using Jehualli.TicketSystem.Api.Models;

namespace Jehualli.TicketSystem.Api.Tests.Integration;

// End-to-end over HTTP: routing, JWT auth, role attributes, validation, EF Core on SQL Server
// and the dashboard stored procedure. Uses the demo accounts created by DbSeeder.
public class ApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static void RequireSqlServer() =>
        Skip.If(ApiFactory.ServerConnection is null, "Set TEST_SQLSERVER to run integration tests");

    private static readonly CreateTicketRequest SampleTicket =
        new("Checkout returns 500", "Steps: add item, pay", TicketPriority.Critical, TicketCategory.Bug, null);

    [SkippableFact]
    public async Task Login_with_wrong_password_returns_401()
    {
        RequireSqlServer();
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin@company.com", "wrong"), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Requests_without_a_token_return_401()
    {
        RequireSqlServer();
        var response = await api.CreateClient().GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Invalid_body_returns_400_with_field_errors()
    {
        RequireSqlServer();
        var alice = await api.LoginAsync("client1@acme.com", "Client@123");

        var response = await alice.PostAsJsonAsync("/api/tickets", new { title = "", priority = "HIGH" }, ApiFactory.Json);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("title", error!.FieldErrors!.Keys);
        Assert.Contains("category", error.FieldErrors.Keys);
    }

    [SkippableFact]
    public async Task Client_creates_ticket_and_another_client_gets_403()
    {
        RequireSqlServer();
        var alice = await api.LoginAsync("client1@acme.com", "Client@123");
        var bob = await api.LoginAsync("client2@techstart.io", "Client@123");

        var created = await alice.PostAsJsonAsync("/api/tickets", SampleTicket, ApiFactory.Json);
        var ticket = await created.Content.ReadFromJsonAsync<TicketResponse>(ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/tickets/{ticket!.Id}", created.Headers.Location!.AbsolutePath);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync($"/api/tickets/{ticket.Id}")).StatusCode);
    }

    [SkippableFact]
    public async Task Role_attributes_block_clients_from_staff_endpoints()
    {
        RequireSqlServer();
        var alice = await api.LoginAsync("client1@acme.com", "Client@123");

        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/dashboard/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/clients")).StatusCode);
    }

    [SkippableFact]
    public async Task Dashboard_counts_come_from_the_stored_procedure()
    {
        RequireSqlServer();
        var alice = await api.LoginAsync("client1@acme.com", "Client@123");
        var agent = await api.LoginAsync("agent@company.com", "Agent@123");

        var before = await agent.GetFromJsonAsync<DashboardStatsResponse>("/api/dashboard/stats", ApiFactory.Json);
        (await alice.PostAsJsonAsync("/api/tickets", SampleTicket, ApiFactory.Json)).EnsureSuccessStatusCode();
        var after = await agent.GetFromJsonAsync<DashboardStatsResponse>("/api/dashboard/stats", ApiFactory.Json);

        Assert.Equal(before!.TotalTickets + 1, after!.TotalTickets);
        Assert.Equal(before.TicketsByPriority[TicketPriority.Critical] + 1, after.TicketsByPriority[TicketPriority.Critical]);
        Assert.Equal(before.TicketsByStatus[TicketStatus.Open] + 1, after.TicketsByStatus[TicketStatus.Open]);
    }

    [SkippableFact]
    public async Task Health_endpoint_reports_database_is_reachable()
    {
        RequireSqlServer();
        var response = await api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
