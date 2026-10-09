using System.ComponentModel.DataAnnotations;
using Jehualli.TicketSystem.Api.Models;

namespace Jehualli.TicketSystem.Api.Dtos;

// ---- Auth ----

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string Token, string TokenType, Guid UserId, string Name, string Email, UserRole Role);

// ---- Users ----

public record UserResponse(Guid Id, string Name, string Email, UserRole Role, bool Active, DateTime CreatedAt);

public record CreateUserRequest(
    [Required, MaxLength(100)] string Name,
    [Required, EmailAddress, MaxLength(150)] string Email,
    [Required, MinLength(8)] string Password,
    [Required] UserRole? Role);

public record UpdateUserRequest(
    [MaxLength(100)] string? Name,
    UserRole? Role,
    bool? Active);

// ---- Clients ----

public record ClientResponse(Guid Id, string CompanyName, string ContactName, string Email, string? Phone, bool Active);

public record CreateClientRequest(
    [Required, MaxLength(150)] string CompanyName,
    [Required, MaxLength(100)] string ContactName,
    [Required, EmailAddress, MaxLength(150)] string Email,
    [MaxLength(30)] string? Phone,
    Guid? UserId);

public record UpdateClientRequest(
    [MaxLength(150)] string? CompanyName,
    [MaxLength(100)] string? ContactName,
    [MaxLength(30)] string? Phone,
    bool? Active);

// ---- Tickets ----

public record UserSummary(Guid Id, string Name, string Email);

public record ClientSummary(Guid Id, string CompanyName);

public record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    TicketCategory Category,
    ClientSummary Client,
    UserSummary? Assignee,
    UserSummary? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    int CommentCount);

public record CreateTicketRequest(
    [Required, MaxLength(255)] string Title,
    [Required] string Description,
    [Required] TicketPriority? Priority,
    [Required] TicketCategory? Category,
    Guid? ClientId);

public record UpdateTicketRequest(
    [MaxLength(255)] string? Title,
    string? Description,
    TicketPriority? Priority,
    TicketCategory? Category,
    TicketStatus? Status,
    Guid? AssigneeId);

public record AssignTicketRequest([Required] Guid? AssigneeId);

public class TicketQuery
{
    public TicketStatus? Status { get; init; }
    public TicketPriority? Priority { get; init; }
    public TicketCategory? Category { get; init; }
    public Guid? ClientId { get; init; }
    public Guid? AssigneeId { get; init; }

    [Range(0, int.MaxValue)]
    public int Page { get; init; } = 0;

    [Range(1, 100)]
    public int Size { get; init; } = 20;

    public string SortBy { get; init; } = "createdAt";
    public string SortDir { get; init; } = "desc";
}

public record PageResponse<T>(IReadOnlyList<T> Content, int Page, int Size, int TotalElements, int TotalPages);

// ---- Comments ----

public record CommentResponse(Guid Id, Guid TicketId, UserSummary? Author, string Content, bool Internal, DateTime CreatedAt);

public record CreateCommentRequest(
    [Required, MaxLength(4000)] string Content,
    bool Internal = false);

// ---- Dashboard ----

public record DashboardStatsResponse(
    int TotalTickets,
    IReadOnlyDictionary<TicketStatus, int> TicketsByStatus,
    IReadOnlyDictionary<TicketPriority, int> TicketsByPriority,
    IReadOnlyList<TicketResponse> RecentTickets);
