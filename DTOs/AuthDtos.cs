namespace CinemaBooking.DTOs
{

    public record RegisterRequest(string Email, string Password, string Name);

    public record LoginRequest(string Email, string Password);

    public record AuthResponse(bool Success, string Message);

    public record JwtLoginResponse(bool Success, string Message, string? Token = null);
}
