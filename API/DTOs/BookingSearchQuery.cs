using API.Models;

namespace API.DTOs; 
public record BookingSearchQuery( 
string?      
RoomName, 
BookingType? Type, 
DateTime?    From, 
DateTime?    To 
);