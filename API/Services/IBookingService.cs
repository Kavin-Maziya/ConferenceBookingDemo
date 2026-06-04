using API.DTOs;

namespace API.Services;

public interface IBookingService
{
    Task<IEnumerable<BookingResponse>> GetAllAsync();
    Task<BookingDetailResponse>
    GetByIdAsync(Guid id);
    Task<IEnumerable<BookingResponse>> SearchAsync(BookingSearchQuery query);
   
    Task<BookingResponse> CreateAsync( CreateBookingRequest request);
    Task<BookingResponse> UpdateAsync(Guid id,  CreateBookingRequest request);
    Task DeleteAsync(Guid id);
}