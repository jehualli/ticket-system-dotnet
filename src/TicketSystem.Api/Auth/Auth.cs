using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Jehualli.TicketSystem.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Jehualli.TicketSystem.Api.Auth;

// Who is making the request. Services take this instead of HttpContext so they are easy to unit test.
public record CurrentUser(Guid Id, UserRole Role)
{
    public bool IsStaff => Role is UserRole.Admin or UserRole.Agent;
}

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "ticket-system";
    public string Audience { get; set; } = "ticket-system";

    // HMAC-SHA256 needs at least 32 bytes.
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 60 * 24;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

public static class AppClaims
{
    public const string Role = "role";
    public const string Name = "name";
}

public class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public string CreateToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(AppClaims.Name, user.Name),
            new Claim(AppClaims.Role, user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: new SigningCredentials(_options.SigningKey(), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static CurrentUser ToCurrentUser(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                 ?? throw new InvalidOperationException("Token has no subject claim");
        var role = principal.FindFirstValue(AppClaims.Role)
                   ?? throw new InvalidOperationException("Token has no role claim");
        return new CurrentUser(Guid.Parse(id), Enum.Parse<UserRole>(role));
    }
}
