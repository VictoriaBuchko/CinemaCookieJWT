namespace CinemaBooking.Models
{
    public class MovieShow
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int AvailableSeats { get; set; }
    }
}
