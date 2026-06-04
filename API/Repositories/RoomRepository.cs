using API.Data;
using API.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Repositories;

public class RoomRepository(BookingDbContext db) : IRoomRepository
{
    public  async Task<Room?> GetByIdAsync(Guid id)
    {
        var room = await db.Rooms.FirstOrDefaultAsync( r =>  r.Id == id);
        
        return room; 

    }
}