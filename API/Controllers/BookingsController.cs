using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using API.Models; 

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{

  [HttpGet]
  public async Task<ActionResult<IEnumerable<Booking>>>GetBookingsAsync()
    {
        //await does block a thread
        // pause this method,return the thread to the pool
        // return here when the I/O is done
        await Task.Delay(200); // await _dbContext.Bookings.ToListAsync();
        return BookingStore.bookings; 
        
    }
    //Get bookings by Id, 


}
