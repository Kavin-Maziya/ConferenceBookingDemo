using API.Models;

namespace API.Repositories;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(Guid id);
}