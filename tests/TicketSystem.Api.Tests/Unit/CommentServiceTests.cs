using Jehualli.TicketSystem.Api.Dtos;
using Jehualli.TicketSystem.Api.Errors;
using Jehualli.TicketSystem.Api.Models;
using Jehualli.TicketSystem.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jehualli.TicketSystem.Api.Tests.Unit;

public class CommentServiceTests : IAsyncLifetime
{
    private TestDb _t = null!;
    private TicketService _tickets = null!;
    private CommentService _comments = null!;
    private Guid _ticketId;

    public async Task InitializeAsync()
    {
        _t = await TestDb.CreateAsync();
        _tickets = new TicketService(_t.Db, NullLogger<TicketService>.Instance);
        _comments = new CommentService(_t.Db, _tickets);

        var ticket = await _tickets.CreateAsync(
            new CreateTicketRequest("Broken", "It broke", TicketPriority.High, TicketCategory.Bug, null), _t.Alice);
        _ticketId = ticket.Id;
        await _tickets.AssignAsync(_ticketId, _t.Developer.Id, _t.Agent);
    }

    public async Task DisposeAsync() => await _t.DisposeAsync();

    [Fact]
    public async Task Clients_do_not_see_internal_comments()
    {
        await _comments.AddAsync(_ticketId, new CreateCommentRequest("Looking into it"), _t.Developer);
        await _comments.AddAsync(_ticketId, new CreateCommentRequest("Root cause is the cache", Internal: true), _t.Developer);

        var forClient = await _comments.ListAsync(_ticketId, _t.Alice);
        var forStaff = await _comments.ListAsync(_ticketId, _t.Agent);

        Assert.Single(forClient);
        Assert.Equal(2, forStaff.Count);
    }

    [Fact]
    public async Task Clients_cannot_post_internal_comments()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _comments.AddAsync(_ticketId, new CreateCommentRequest("psst", Internal: true), _t.Alice));
    }

    [Fact]
    public async Task Unassigned_developer_cannot_comment()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _comments.AddAsync(_ticketId, new CreateCommentRequest("hi"), _t.OtherDeveloper));
    }

    [Fact]
    public async Task Other_client_cannot_read_comments()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _comments.ListAsync(_ticketId, _t.Bob));
    }

    [Fact]
    public async Task Only_author_or_admin_can_delete_a_comment()
    {
        var comment = await _comments.AddAsync(_ticketId, new CreateCommentRequest("mine"), _t.Alice);

        await Assert.ThrowsAsync<ForbiddenException>(() => _comments.DeleteAsync(_ticketId, comment.Id, _t.Agent));
        await _comments.DeleteAsync(_ticketId, comment.Id, _t.Alice);

        Assert.Empty(await _comments.ListAsync(_ticketId, _t.Admin));
    }

    [Fact]
    public async Task Comment_count_appears_on_the_ticket()
    {
        await _comments.AddAsync(_ticketId, new CreateCommentRequest("one"), _t.Alice);
        await _comments.AddAsync(_ticketId, new CreateCommentRequest("two"), _t.Developer);

        var ticket = await _tickets.GetAsync(_ticketId, _t.Admin);

        Assert.Equal(2, ticket.CommentCount);
    }
}
