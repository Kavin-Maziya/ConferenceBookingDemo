using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API.DTOs;

namespace API.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _config;

    // Hardcoded user store — simulates a database Users table.
    // Each tuple represents one account per role so every access level can be tested in Scalar.
    // Week 2 replaces this with an EF Core query against a real Users table.
    private static readonly (string Username, string Password, string Role)[] _users =
    [
        ("admin",        "password123", "Admin"),
        ("receptionist", "password123", "Receptionist"),
        ("facilities",   "password123", "FacilitiesManager"),
        ("alice",        "password123", "Employee"),
    ];

    // IConfiguration is injected so the JWT secret is read from appsettings,
    // not hardcoded anywhere in source code.
    public AuthService(IConfiguration config)
    {
        _config = config;
    }

    public LoginResponse? Login(LoginRequest request)
    {
        // Find a matching user by username and password.
        // In production this would compare a submitted password against a stored hash
        // (BCrypt / Argon2) — never plain-text string equality on real passwords.
        var user = _users.FirstOrDefault(u =>
            u.Username == request.username && u.Password == request.Password);

        // Return null so the controller decides the HTTP response.
        // The service has no knowledge of HTTP — it only knows "valid" or "not valid".
        if (user == default)
            return null;

        var token = BuildToken(user.Username, user.Role);
        return new LoginResponse(token);
    }

    // Constructs and signs the JWT.
    // Kept private — callers only need Login(); token internals are an implementation detail.
    private string BuildToken(string username, string role)
    {
        // Claims are facts about the user that are embedded in the token payload.
        // Every request that carries this token will have these values available
        // via User.FindFirstValue(...) inside any controller.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username), // Who the token belongs to
            new Claim(ClaimTypes.Role, role)                  // Which role gates they can pass
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]!));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2), // UtcNow — always timezone-safe
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
