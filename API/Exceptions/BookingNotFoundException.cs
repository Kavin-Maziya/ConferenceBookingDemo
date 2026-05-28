namespace API.Exceptions;

public class BookingNotFoundException : Exception
{
    public BookingNotFoundException(Guid id): base($"bOOKING with ID {id} was not found at all ")
    {
        
    }
}