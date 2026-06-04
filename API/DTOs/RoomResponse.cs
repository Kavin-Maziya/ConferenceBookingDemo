namespace API.DTOs;

public record RoomResponse(
    Guid Id, 
    string Name,
    string Floor, // Or int, depending on how your entity is structured
    int Capacity,
    bool IsAvailable
);