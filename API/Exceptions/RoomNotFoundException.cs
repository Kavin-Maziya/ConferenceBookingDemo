namespace API.Exceptions;

public class RoomNotFoundException : Exception
{
    public RoomNotFoundException(Guid roomId)
        : base($"Room with ID '{roomId}' was not found") { }
}

