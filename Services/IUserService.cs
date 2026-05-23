using CinemaBooking.Models;

namespace CinemaBooking.Services
{
    public interface IUserService
    {
        User? Register(string email, string password, string name);
        User? Authenticate(string email, string password);
        User? GetById(int id);
    }
}
