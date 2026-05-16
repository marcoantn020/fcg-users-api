namespace UsersAPI.API.Endpoints;

public class AuthDtos
{
    public record RegisterRequest(string Email, string Password, string DisplayName);
    public record LoginRequest(string Email, string Password);
    public record AuthResponse(Guid UserId, string Email, string DisplayName, string AccessToken);
}