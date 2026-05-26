using API.Models;

public static class BookingStore
{
    public static readonly List<Booking> bookings = new()
    {
        new Booking (
          Guid.NewGuid(),
          
          ".Net 10 Perfomance Deep Dive",
          "Jane Doe",
          "Room A",
         DateTime.UtcNow.AddDays(5)
        ),
        new Booking (
          Guid.NewGuid(),
          
          "Async/Await: Handling code in parallel",
          "John Smith",
          "Room B",
         DateTime.UtcNow.AddDays(5).AddHours(2)
        )
    };
}