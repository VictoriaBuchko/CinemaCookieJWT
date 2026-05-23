using CinemaBooking.Models;

namespace CinemaBooking.Services
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}
