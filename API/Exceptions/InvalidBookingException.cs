namespace API.Exceptions;

// Thrown when a booking request violates a domain rule that is not
// a conflict with an existing booking — e.g. end time is before start time.
// GlobalExceptionHandler maps this to HTTP 400 Bad Request.
public class InvalidBookingException : Exception
{
    public InvalidBookingException(string message) : base(message) { }
}
