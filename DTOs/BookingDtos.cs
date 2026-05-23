namespace CinemaBooking.DTOs
{
    public record CreateBookingRequest(int MovieShowId, int NumberOfSeats);

    public record BookingResponse(
        int Id,
        int UserId,
        int MovieShowId,
        string MovieTitle,
        int NumberOfSeats,
        DateTime BookingTime);
}
