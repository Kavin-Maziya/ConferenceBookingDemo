using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using API.DTOs;
using API.Services;

namespace API.Controllers;

// CHANGED: Removed all JWT construction, credential checking, and hardcoded key from this class.
// The controller now has one job: receive an HTTP request, delegate to the service, return a response.
// All auth logic (user lookup, token building, signing) lives in AuthService.

[ApiController]
[Route("api/[controller]")]
// CHANGED: Primary constructor — matches the project pattern used in GlobalExceptionHandler.
// The controller depends on IAuthService (the interface), not AuthService (the concrete class).
// If the user store moves to a database in Week 2, only AuthService changes.
public class AuthController(IAuthService authService) : ControllerBase
{
    // POST /api/auth/login
    // CHANGED: Was 40+ lines of credential checks, key construction, and JWT assembly inline.
    // Now delegates entirely to the service and maps the result to an HTTP response.
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var response = authService.Login(request);

        // Service returns null for any invalid credential combination.
        // We never reveal whether the username or the password was wrong.
        if (response is null)
            return Unauthorized(); // 401

        return Ok(response); // 200 — body: { "token": "eyJ..." }
    }

    // GET /api/auth/me — unchanged in behaviour.
    // Returns the identity of the currently authenticated caller by reading the claims
    // that UseAuthentication decoded from their JWT — no service call needed.
    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        var username = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var role     = User.FindFirstValue(ClaimTypes.Role);

        return Ok(new { username, role });
    }
}
