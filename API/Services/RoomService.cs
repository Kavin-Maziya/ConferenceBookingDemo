using API.Repositories;
using API.DTOs;
using API.Exceptions; 

namespace API.Services;  
public class RoomService(IRoomRepository roomRepository) : IRoomService
{
    /*public async Task<IEnumerable<RoomResponse>> GetAllAsync()
    {
        // Assuming your repository handles the projection to DTOs for list reads,
        // exactly like BookingRepository.GetAllAsync() does.
        return await roomRepository.GetAllAsync();
    } */

    public async Task<RoomResponse> GetByIdAsync(Guid id)
    {
        // Fetch the data from the repository
        var room = await roomRepository.GetByIdAsync(id);

        // Enforce the business rule: if it doesn't exist, throw the domain exception
        if (room is null)
        {
            throw new RoomNotFoundException(id);
        }

        // Map the domain entity to a response DTO before returning it to the controller
        return new RoomResponse(
            room.Id, 
            room.Name, 
            room.Floor, 
            room.Capacity, 
            room.IsAvailable
        );
    }
}


