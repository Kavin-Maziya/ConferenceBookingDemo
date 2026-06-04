using API.DTOs; 
namespace API.Services;

public interface IRoomService
{
    //Task<IEnumerable<RoomResponse>> GetAllAsync();
    Task<RoomResponse> GetByIdAsync(Guid id);
}