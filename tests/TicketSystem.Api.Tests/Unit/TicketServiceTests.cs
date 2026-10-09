using Jehualli.TicketSystem.Api.Dtos;
using Jehualli.TicketSystem.Api.Errors;
using Jehualli.TicketSystem.Api.Models;
using Jehualli.TicketSystem.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jehualli.TicketSystem.Api.Tests.Unit;

public class TicketServiceTests : IAsyncLifetime
{
    private TestDb _t = null!;
    private TicketService _tickets = null!;

    public async Task InitializeAsync()
    {
        _t = await TestDb.CreateAsync();
        _tickets = new TicketService(_t.Db, NullLogger<TicketService>.Instance);
    }

    public async Task DisposeAsync() => await _t.DisposeAsync();

    private static CreateTicketRequest NewTicket(string title = "Printer on fire", TicketPriority priority = TicketPriority.High, Guid? clientId = null) =>
        new(title, "Details", priority, TicketCategory.Bug, clientId);

    [Fact]
    public async Task Client_creates_ticket_for_their_own_company()
    {
        var ticket = await _tickets.CreateAsync(NewTicket(), _t.Alice);

        Assert.Equal(_t.AcmeId, ticket.Client.Id);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(_t.Alice.Id, ticket.CreatedBy!.Id);
    }

    [Fact]
    public async Task Client_cannot_pick_another_company_by_passing_clientId()
    {
        var ticket = await _tickets.CreateAsync(NewTicket(clientId: _t.TechStartId), _t.Alice);

        Assert.Equal(_t.AcmeId, ticket.Client.Id);
    }

    [Fact]
    public async Task Staff_must_say_which_client_the_ticket_is_for()
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _tickets.CreateAsync(NewTicket(), _t.Agent));
        Assert.Contains("clientId", ex.Message);
    }

    [Fact]
    public async Task Developer_cannot_create_tickets()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _tickets.CreateAsync(NewTicket(clientId: _t.AcmeId), _t.Developer));
    }

    [Fact]
    public async Task Client_cannot_view_another_companys_ticket()
    {
        var ticket = await _tickets.CreateAsync(NewTicket(), _t.Alice);

        await Assert.ThrowsAsync<ForbiddenException>(() => _tickets.GetAsync(ticket.Id, _t.Bob));
    }

    [Fact]
    public async Task Search_only_returns_tickets_the_user_may_see()
    {
        await _tickets.CreateAsync(NewTicket("Acme 1"), _t.Alice);
        await _tickets.CreateAsync(NewTicket("Acme 2"), _t.Alice);
        var techStart = await _tickets.CreateAsync(NewTicket("TechStart 1"), _t.Bob);
        await _tickets.AssignAsync(techStart.Id, _t.Developer.Id, _t.Agent);

        var query = new TicketQuery();
        Assert.Equal(2, (await _tickets.SearchAsync(query, _t.Alice)).TotalElements);
        Assert.Equal(1, (await _tickets.SearchAsync(query, _t.Bob)).TotalElements);
        Assert.Equal(1, (await _tickets.SearchAsync(query, _t.Developer)).TotalElements);
        Assert.Equal(0, (await _tickets.SearchAsync(query, _t.OtherDeveloper)).TotalElements);
        Assert.Equal(3, (await _tickets.SearchAsync(query, _t.Admin)).TotalElements);
    }

    [Fact]
    public async Task Search_filters_and_pages()
    {
        for (var i = 0; i < 5; i++) await _tickets.CreateAsync(NewTicket($"High {i}"), _t.Alice);
        await _tickets.CreateAsync(NewTicket("Low one", TicketPriority.Low), _t.Alice);

        var page = await _tickets.SearchAsync(new TicketQuery { Priority = TicketPriority.High, Page = 1, Size = 2 }, _t.Admin);

        Assert.Equal(5, page.TotalElements);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Content.Count);
        Assert.All(page.Content, t => Assert.Equal(TicketPriority.High, t.Priority));
    }

    [Fact]
    public async Task Sorting_by_priority_uses_severity_not_alphabetical_order()
    {
        await _tickets.CreateAsync(NewTicket("m", TicketPriority.Medium), _t.Alice);
        await _tickets.CreateAsync(NewTicket("c", TicketPriority.Critical), _t.Alice);
        await _tickets.CreateAsync(NewTicket("l", TicketPriority.Low), _t.Alice);
        await _tickets.CreateAsync(NewTicket("h", TicketPriority.High), _t.Alice);

        var page = await _tickets.SearchAsync(new TicketQuery { SortBy = "priority", SortDir = "desc" }, _t.Admin);

        Assert.Equal(
            [TicketPriority.Critical, TicketPriority.High, TicketPriority.Medium, TicketPriority.Low],
            page.Content.Select(t => t.Priority));
    }

    [Fact]
    public async Task Assigning_an_open_ticket_moves_it_to_in_progress()
    {
        var ticket = await _tickets.CreateAsync(NewTicket(), _t.Alice);

        var assigned = await _tickets.AssignAsync(ticket.Id, _t.Developer.Id, _t.Agent);

        Assert.Equal(TicketStatus.InProgress, assigned.Status);
        Assert.Equal(_t.Developer.Id, assigned.Assignee!.Id);
    }

    [Fact]
    public async Task Tickets_cannot_be_assigned_to_clients()
    {
        var ticket = await _tickets.CreateAsync(NewTicket(), _t.Alice);

        await Assert.ThrowsAsync<BadRequestException>(() => _tickets.AssignAsync(ticket.Id, _t.Bob.Id, _t.Agent));
    }

    [Fact]
    public async Task Resolving_sets_resolvedAt_once()
    {
        var ticket = await _tickets.CreateAsync(NewTicket(), _t.Alice);
        await _tickets.AssignAsync(ticket.Id, _t.Developer.Id, _t.Agent);

        var resolved = await _tickets.UpdateAsync(ticket.Id, new UpdateTicketRequest(null, null, null, null, TicketStatus.Resolved, null), _t.Developer);
        var closed = await _tickets.UpdateAsync(ticket.Id, new UpdateTicketRequest(null, null, null, null, TicketStatus.Closed, null), _t.Agent);

        Assert.NotNull(resolved.ResolvedAt);
        Assert.Equal(resolved.ResolvedAt, closed.ResolvedAt);
    }

    [Fact]
    public async Task Client_update_only_changes_title_and_description()
    {
        var ticket = await _tickets.CreateAsync(NewTicket(), _t.Alice);

        var updated = await _tickets.UpdateAsync(ticket.Id,
            new UpdateTicketRequest("New title", null, TicketPriority.Critical, null, TicketStatus.Closed, null), _t.Alice);

        Assert.Equal("New title", updated.Title);
        Assert.Equal(TicketPriority.High, updated.Priority);
        Assert.Equal(TicketStatus.Open, updated.Status);
    }

    [Fact]
    public async Task Only_admin_can_delete()
    {
        var ticket = await _tickets.CreateAsync(NewTicket(), _t.Alice);

        await Assert.ThrowsAsync<ForbiddenException>(() => _tickets.DeleteAsync(ticket.Id, _t.Agent));
        await _tickets.DeleteAsync(ticket.Id, _t.Admin);
        await Assert.ThrowsAsync<NotFoundException>(() => _tickets.GetAsync(ticket.Id, _t.Admin));
    }
}
