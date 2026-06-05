using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using API.DTOs;
using API.Services;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController(IRoomService roomService) : ControllerBase
{
    // GET /api/rooms/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoomResponse>> GetRoomByIdAsync(Guid id)
    {
        // RoomNotFoundException thrown by service and caught by GlobalExceptionHandler → 404
        var room = await roomService.GetByIdAsync(id);
        return Ok(room);
    }

}