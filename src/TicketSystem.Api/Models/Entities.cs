namespace Jehualli.TicketSystem.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Client
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    // The login account of the client's contact person, if they have one.
    public Guid? UserId { get; set; }
    public User? User { get; set; }
}

public class Ticket
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketCategory Category { get; set; } = TicketCategory.Support;

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public Guid? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public List<Comment> Comments { get; set; } = [];
}

public class Comment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public Guid? AuthorId { get; set; }
    public User? Author { get; set; }

    public string Content { get; set; } = "";

    // Internal comments are staff-only notes; clients never see them.
    public bool Internal { get; set; }
    public DateTime CreatedAt { get; set; }
}
