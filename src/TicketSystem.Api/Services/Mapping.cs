using System.Linq.Expressions;
using Jehualli.TicketSystem.Api.Dtos;
using Jehualli.TicketSystem.Api.Models;

namespace Jehualli.TicketSystem.Api.Services;

// LINQ projections. EF translates these to a single SELECT, so listing tickets
// does not run one extra query per row to count comments.
public static class Mapping
{
    public static readonly Expression<Func<Ticket, TicketResponse>> ToTicketResponse = t => new TicketResponse(
        t.Id,
        t.Title,
        t.Description,
        t.Status,
        t.Priority,
        t.Category,
        new ClientSummary(t.Client.Id, t.Client.CompanyName),
        t.Assignee == null ? null : new UserSummary(t.Assignee.Id, t.Assignee.Name, t.Assignee.Email),
        t.CreatedBy == null ? null : new UserSummary(t.CreatedBy.Id, t.CreatedBy.Name, t.CreatedBy.Email),
        t.CreatedAt,
        t.UpdatedAt,
        t.ResolvedAt,
        t.Comments.Count);

    public static readonly Expression<Func<Comment, CommentResponse>> ToCommentResponse = c => new CommentResponse(
        c.Id,
        c.TicketId,
        c.Author == null ? null : new UserSummary(c.Author.Id, c.Author.Name, c.Author.Email),
        c.Content,
        c.Internal,
        c.CreatedAt);

    public static UserResponse ToResponse(this User u) =>
        new(u.Id, u.Name, u.Email, u.Role, u.Active, u.CreatedAt);

    public static ClientResponse ToResponse(this Client c) =>
        new(c.Id, c.CompanyName, c.ContactName, c.Email, c.Phone, c.Active);
}
