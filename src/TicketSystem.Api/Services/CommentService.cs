using Jehualli.TicketSystem.Api.Auth;
using Jehualli.TicketSystem.Api.Data;
using Jehualli.TicketSystem.Api.Dtos;
using Jehualli.TicketSystem.Api.Errors;
using Jehualli.TicketSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Jehualli.TicketSystem.Api.Services;

public class CommentService(AppDbContext db, TicketService tickets)
{
    public async Task<IReadOnlyList<CommentResponse>> ListAsync(Guid ticketId, CurrentUser user, CancellationToken ct = default)
    {
        var ticket = await tickets.FindAsync(ticketId, ct);
        await tickets.EnsureCanAccessAsync(ticket, user, "view", ct);

        var comments = db.Comments.AsNoTracking().Where(c => c.TicketId == ticketId);
        if (user.Role == UserRole.Client) comments = comments.Where(c => !c.Internal);

        return await comments
            .OrderBy(c => c.CreatedAt)
            .Select(Mapping.ToCommentResponse)
            .ToListAsync(ct);
    }

    public async Task<CommentResponse> AddAsync(Guid ticketId, CreateCommentRequest request, CurrentUser user, CancellationToken ct = default)
    {
        var ticket = await tickets.FindAsync(ticketId, ct);
        await tickets.EnsureCanAccessAsync(ticket, user, "comment on", ct);

        if (user.Role == UserRole.Client && request.Internal)
            throw new BadRequestException("Clients cannot create internal comments");

        var comment = new Comment
        {
            TicketId = ticketId,
            AuthorId = user.Id,
            Content = request.Content,
            Internal = request.Internal,
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);

        return await db.Comments.AsNoTracking()
            .Where(c => c.Id == comment.Id)
            .Select(Mapping.ToCommentResponse)
            .SingleAsync(ct);
    }

    public async Task DeleteAsync(Guid ticketId, Guid commentId, CurrentUser user, CancellationToken ct = default)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.TicketId == ticketId, ct)
                      ?? throw new NotFoundException("Comment", commentId);

        if (user.Role != UserRole.Admin && comment.AuthorId != user.Id)
            throw new ForbiddenException("You can only delete your own comments");

        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);
    }
}
