using Jehualli.TicketSystem.Api.Auth;
using Jehualli.TicketSystem.Api.Data;
using Jehualli.TicketSystem.Api.Dtos;
using Jehualli.TicketSystem.Api.Errors;
using Jehualli.TicketSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Jehualli.TicketSystem.Api.Services;

public class TicketService(AppDbContext db, ILogger<TicketService> logger)
{
    public async Task<TicketResponse> CreateAsync(CreateTicketRequest request, CurrentUser user, CancellationToken ct = default)
    {
        Guid clientId;
        if (user.Role == UserRole.Client)
        {
            clientId = await ClientIdForUserAsync(user, ct);
        }
        else if (user.IsStaff)
        {
            clientId = request.ClientId
                       ?? throw new BadRequestException("clientId is required when creating a ticket as ADMIN or AGENT");
            if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct))
                throw new NotFoundException("Client", clientId);
        }
        else
        {
            throw new ForbiddenException("Developers cannot create tickets");
        }

        var ticket = new Ticket
        {
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority!.Value,
            Category = request.Category!.Value,
            ClientId = clientId,
            CreatedById = user.Id,
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Ticket {TicketId} created by {UserId}", ticket.Id, user.Id);
        return await GetResponseAsync(ticket.Id, ct);
    }

    public async Task<PageResponse<TicketResponse>> SearchAsync(TicketQuery query, CurrentUser user, CancellationToken ct = default)
    {
        var tickets = await VisibleToAsync(user, ct);

        if (query.Status is { } status) tickets = tickets.Where(t => t.Status == status);
        if (query.Priority is { } priority) tickets = tickets.Where(t => t.Priority == priority);
        if (query.Category is { } category) tickets = tickets.Where(t => t.Category == category);
        if (query.ClientId is { } clientId) tickets = tickets.Where(t => t.ClientId == clientId);
        if (query.AssigneeId is { } assigneeId) tickets = tickets.Where(t => t.AssigneeId == assigneeId);

        var total = await tickets.CountAsync(ct);
        var content = await Sort(tickets, query.SortBy, query.SortDir)
            .Skip(query.Page * query.Size)
            .Take(query.Size)
            .Select(Mapping.ToTicketResponse)
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(total / (double)query.Size);
        return new PageResponse<TicketResponse>(content, query.Page, query.Size, total, totalPages);
    }

    public async Task<TicketResponse> GetAsync(Guid id, CurrentUser user, CancellationToken ct = default)
    {
        var ticket = await FindAsync(id, ct);
        await EnsureCanAccessAsync(ticket, user, "view", ct);
        return await GetResponseAsync(id, ct);
    }

    public async Task<TicketResponse> UpdateAsync(Guid id, UpdateTicketRequest request, CurrentUser user, CancellationToken ct = default)
    {
        var ticket = await FindAsync(id, ct);
        await EnsureCanAccessAsync(ticket, user, "update", ct);

        // Everyone who can see a ticket can edit its text.
        if (request.Title is not null) ticket.Title = request.Title;
        if (request.Description is not null) ticket.Description = request.Description;

        // Developers can also move it through the workflow.
        if (user.Role != UserRole.Client && request.Status is { } status) ticket.Status = status;

        // Only staff can triage and assign.
        if (user.IsStaff)
        {
            if (request.Priority is { } priority) ticket.Priority = priority;
            if (request.Category is { } category) ticket.Category = category;
            if (request.AssigneeId is { } assigneeId) ticket.AssigneeId = await FindAssignableUserAsync(assigneeId, ct);
        }

        await db.SaveChangesAsync(ct);
        return await GetResponseAsync(id, ct);
    }

    public async Task<TicketResponse> AssignAsync(Guid id, Guid assigneeId, CurrentUser user, CancellationToken ct = default)
    {
        if (!user.IsStaff) throw new ForbiddenException("Only ADMIN and AGENT can assign tickets");

        var ticket = await FindAsync(id, ct);
        ticket.AssigneeId = await FindAssignableUserAsync(assigneeId, ct);
        if (ticket.Status == TicketStatus.Open) ticket.Status = TicketStatus.InProgress;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Ticket {TicketId} assigned to {AssigneeId}", id, assigneeId);
        return await GetResponseAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CurrentUser user, CancellationToken ct = default)
    {
        if (user.Role != UserRole.Admin) throw new ForbiddenException("Only ADMIN can delete tickets");

        var ticket = await FindAsync(id, ct);
        db.Tickets.Remove(ticket);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Ticket {TicketId} deleted by {UserId}", id, user.Id);
    }

    // Shared with CommentService: the same visibility rules apply to a ticket's comments.
    internal async Task EnsureCanAccessAsync(Ticket ticket, CurrentUser user, string action, CancellationToken ct)
    {
        switch (user.Role)
        {
            case UserRole.Client when ticket.ClientId != await ClientIdForUserAsync(user, ct):
                throw new ForbiddenException($"You are not allowed to {action} this ticket");
            case UserRole.Developer when ticket.AssigneeId != user.Id:
                throw new ForbiddenException($"Developers can only {action} their assigned tickets");
        }
    }

    internal async Task<Ticket> FindAsync(Guid id, CancellationToken ct) =>
        await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Ticket", id);

    private async Task<IQueryable<Ticket>> VisibleToAsync(CurrentUser user, CancellationToken ct)
    {
        var tickets = db.Tickets.AsNoTracking();
        switch (user.Role)
        {
            case UserRole.Client:
                var clientId = await ClientIdForUserAsync(user, ct);
                return tickets.Where(t => t.ClientId == clientId);
            case UserRole.Developer:
                return tickets.Where(t => t.AssigneeId == user.Id);
            default:
                return tickets;
        }
    }

    private async Task<Guid> ClientIdForUserAsync(CurrentUser user, CancellationToken ct) =>
        await db.Clients.Where(c => c.UserId == user.Id).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct)
        ?? throw new BadRequestException("No client profile found for this user");

    private async Task<Guid> FindAssignableUserAsync(Guid userId, CancellationToken ct)
    {
        var assignee = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                       ?? throw new NotFoundException("User", userId);
        if (assignee.Role == UserRole.Client || !assignee.Active)
            throw new BadRequestException("Tickets can only be assigned to active staff or developers");
        return assignee.Id;
    }

    private Task<TicketResponse> GetResponseAsync(Guid id, CancellationToken ct) =>
        db.Tickets.AsNoTracking().Where(t => t.Id == id).Select(Mapping.ToTicketResponse).SingleAsync(ct);

    // Only whitelisted columns can be sorted on, so the query string can't reach arbitrary fields.
    private static IQueryable<Ticket> Sort(IQueryable<Ticket> tickets, string sortBy, string sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy.ToLowerInvariant() switch
        {
            "title" => desc ? tickets.OrderByDescending(t => t.Title) : tickets.OrderBy(t => t.Title),
            "status" => desc ? tickets.OrderByDescending(StatusRank) : tickets.OrderBy(StatusRank),
            "priority" => desc ? tickets.OrderByDescending(PriorityRank) : tickets.OrderBy(PriorityRank),
            "updatedat" => desc ? tickets.OrderByDescending(t => t.UpdatedAt) : tickets.OrderBy(t => t.UpdatedAt),
            _ => desc ? tickets.OrderByDescending(t => t.CreatedAt) : tickets.OrderBy(t => t.CreatedAt),
        };
    }

    // Enums are stored as strings, so sorting the column directly would be alphabetical
    // (Critical, High, Low, Medium). These become a SQL CASE that sorts by severity instead.
    private static readonly System.Linq.Expressions.Expression<Func<Ticket, int>> PriorityRank = t =>
        t.Priority == TicketPriority.Critical ? 3
        : t.Priority == TicketPriority.High ? 2
        : t.Priority == TicketPriority.Medium ? 1
        : 0;

    private static readonly System.Linq.Expressions.Expression<Func<Ticket, int>> StatusRank = t =>
        t.Status == TicketStatus.Open ? 0
        : t.Status == TicketStatus.InProgress ? 1
        : t.Status == TicketStatus.Pending ? 2
        : t.Status == TicketStatus.Resolved ? 3
        : 4;
}
