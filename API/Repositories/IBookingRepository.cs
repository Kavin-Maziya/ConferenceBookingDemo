using API.DTOs;
using API.Models;

namespace API.Repositories;

public interface IBookingRepository
{
    
    Task <IEnumerable<BookingResponse>> GetAllAsync();
    Task<BookingDetailResponse?> GetByIdAsync(Guid id);
    // Returns the tracked domain entity — used by UpdateAsync and DeleteAsync in the service
    // so the Change Tracker can detect mutations and generate the correct SQL.
    Task<Booking?> GetEntityByIdAsync(Guid id);
    Task<IEnumerable<BookingResponse>> SearchAsync(BookingSearchQuery bookingSearchQuery ); 
     Task<bool> HasConflictAsync( 
         Guid roomId, 
       DateTime start, 
       DateTime end, 
      Guid? excludeBookingId = null                                 
     );
     Task<Booking> AddAsync (Booking booking);
     Task<Booking> DeleteAsync(Booking booking);
     Task<Booking> UpdateAsync(Booking booking); 

}