using CinemaBooking.Models;

namespace CinemaBooking.Services
{
    public class UserService : IUserService
    {
        private readonly List<User> _users = new();

        public User? Register(string email, string password, string name)
        {
            if (_users.Any(u => u.Email == email))
                return null;

            var user = new User
            {
                Id = _users.Count + 1,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Name = name
            };

            _users.Add(user);
            return user;
        }

        public User? Authenticate(string email, string password)
        {
            var user = _users.FirstOrDefault(u => u.Email == email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;

            return user;
        }

        public User? GetById(int id) => _users.FirstOrDefault(u => u.Id == id);
    }
}
