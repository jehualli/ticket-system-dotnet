using Jehualli.TicketSystem.Api.Auth;
using Jehualli.TicketSystem.Api.Dtos;
using Jehualli.TicketSystem.Api.Models;
using Jehualli.TicketSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jehualli.TicketSystem.Api.Controllers;

public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Staff = $"{nameof(UserRole.Admin)},{nameof(UserRole.Agent)}";
}

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    [HttpGet("me")]
    [Authorize]
    public Task<UserResponse> Me(CancellationToken ct) => auth.MeAsync(User.ToCurrentUser(), ct);
}

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController(TicketService tickets, CommentService comments) : ControllerBase
{
    [HttpGet]
    public Task<PageResponse<TicketResponse>> Search([FromQuery] TicketQuery query, CancellationToken ct) =>
        tickets.SearchAsync(query, User.ToCurrentUser(), ct);

    [HttpGet("{id:guid}")]
    public Task<TicketResponse> Get(Guid id, CancellationToken ct) => tickets.GetAsync(id, User.ToCurrentUser(), ct);

    [HttpPost]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var created = await tickets.CreateAsync(request, User.ToCurrentUser(), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<TicketResponse> Update(Guid id, UpdateTicketRequest request, CancellationToken ct) =>
        tickets.UpdateAsync(id, request, User.ToCurrentUser(), ct);

    [HttpPut("{id:guid}/assign")]
    [Authorize(Roles = Roles.Staff)]
    public Task<TicketResponse> Assign(Guid id, AssignTicketRequest request, CancellationToken ct) =>
        tickets.AssignAsync(id, request.AssigneeId!.Value, User.ToCurrentUser(), ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tickets.DeleteAsync(id, User.ToCurrentUser(), ct);
        return NoContent();
    }

    [HttpGet("{ticketId:guid}/comments")]
    public Task<IReadOnlyList<CommentResponse>> ListComments(Guid ticketId, CancellationToken ct) =>
        comments.ListAsync(ticketId, User.ToCurrentUser(), ct);

    [HttpPost("{ticketId:guid}/comments")]
    [ProducesResponseType<CommentResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddComment(Guid ticketId, CreateCommentRequest request, CancellationToken ct)
    {
        var created = await comments.AddAsync(ticketId, request, User.ToCurrentUser(), ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpDelete("{ticketId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid ticketId, Guid commentId, CancellationToken ct)
    {
        await comments.DeleteAsync(ticketId, commentId, User.ToCurrentUser(), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UserResponse>> List(CancellationToken ct) => users.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<UserResponse> Get(Guid id, CancellationToken ct) => users.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
    {
        var created = await users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<UserResponse> Update(Guid id, UpdateUserRequest request, CancellationToken ct) =>
        users.UpdateAsync(id, request, ct);
}

[ApiController]
[Route("api/clients")]
[Authorize(Roles = Roles.Staff)]
public class ClientsController(ClientService clients) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ClientResponse>> List(CancellationToken ct) => clients.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<ClientResponse> Get(Guid id, CancellationToken ct) => clients.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateClientRequest request, CancellationToken ct)
    {
        var created = await clients.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<ClientResponse> Update(Guid id, UpdateClientRequest request, CancellationToken ct) =>
        clients.UpdateAsync(id, request, ct);
}

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.Staff)]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet("stats")]
    public Task<DashboardStatsResponse> Stats(CancellationToken ct) => dashboard.GetStatsAsync(ct);
}
