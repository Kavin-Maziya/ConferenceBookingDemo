namespace API.Models;
public record Booking
(
    Guid id, string Title, 
    string Speaker, 
    string Room, 
    DateTime StartTime
); 