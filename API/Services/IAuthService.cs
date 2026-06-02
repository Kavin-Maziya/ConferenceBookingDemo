using API.DTOs;

namespace API.Services;

// Defines the contract for authentication.
// AuthController depends on this interface, not the concrete AuthService class.
// This means JWT logic can be swapped or tested independently of the controller.
public interface IAuthService
{
    // Returns a LoginResponse with a signed JWT on success.
    // Returns null if the credentials are invalid — the controller turns null into a 401.
    LoginResponse? Login(LoginRequest request);
}
