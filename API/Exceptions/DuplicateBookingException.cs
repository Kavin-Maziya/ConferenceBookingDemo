namespace API.Exceptions;

public class DuplicateBookingException : Exception
{
    public DuplicateBookingException(string Room, DateTime time): 
    base($"Room '{Room}' is already booked at {time:HH:mm} on {time:yyyy-MM-dd}")
    {
        
    }
}