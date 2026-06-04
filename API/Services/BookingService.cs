using API.Repositories;
using API.DTOs;
using API.Exceptions;
using API.Models;

namespace API.Services;

public class BookingService(IBookingRepository bookingRepository, IRoomRepository roomRepository) : IBookingService
{
    public Task<IEnumerable<BookingResponse>> GetAllAsync() =>
        bookingRepository.GetAllAsync();

    public Task<IEnumerable<BookingResponse>> SearchAsync(BookingSearchQuery query) =>
        bookingRepository.SearchAsync(query);

    public async Task<BookingDetailResponse> GetByIdAsync(Guid id)
    {
        var booking = await bookingRepository.GetByIdAsync(id);
        if (booking is null)
            throw new BookingNotFoundException(id);

        return booking;
    }

    public async Task<BookingResponse> CreateAsync(CreateBookingRequest request)
    {
        var room = await roomRepository.GetByIdAsync(request.RoomId)
            ?? throw new RoomNotFoundException(request.RoomId);

        // End time must come after start time — checked before hitting the database.
        if (request.EndTime!.Value <= request.StartTime!.Value)
            throw new InvalidBookingException("End time must be after start time.");

        if (await bookingRepository.HasConflictAsync(
                request.RoomId, request.StartTime.Value, request.EndTime.Value))
            throw new DuplicateBookingException(
                room.Name, request.StartTime.Value, request.EndTime.Value);

        var booking = new Booking
        {
            Id             = Guid.NewGuid(),
            Title          = request.Title,
            Description    = request.Description,
            StartTime      = request.StartTime.Value,
            EndTime        = request.EndTime.Value,
            Type           = request.Type,
            OrganizerEmail = request.OrganizerEmail,
            RoomId         = request.RoomId
        };

        await bookingRepository.AddAsync(booking);

        // Re-fetch as a detail response then map down to the summary shape.
        // ToSummary() is defined in DTOs/MappingExtensions.cs.
        return (await bookingRepository.GetByIdAsync(booking.Id))!.ToSummary();
    }

    public async Task<BookingResponse> UpdateAsync(Guid id, CreateBookingRequest request)
    {
        // GetEntityByIdAsync returns a tracked Booking so the Change Tracker
        // can detect property mutations and generate the correct UPDATE statement.
        var booking = await bookingRepository.GetEntityByIdAsync(id)
            ?? throw new BookingNotFoundException(id);

        var room = await roomRepository.GetByIdAsync(request.RoomId)
            ?? throw new RoomNotFoundException(request.RoomId);

        if (request.EndTime!.Value <= request.StartTime!.Value)
            throw new InvalidBookingException("End time must be after start time.");

        // Exclude the booking being updated from the conflict check — a booking
        // never conflicts with its own existing time slot.
        if (await bookingRepository.HasConflictAsync(
                request.RoomId, request.StartTime.Value, request.EndTime.Value,
                excludeBookingId: id))
            throw new DuplicateBookingException(
                room.Name, request.StartTime.Value, request.EndTime.Value);

        booking.Title          = request.Title;
        booking.Description    = request.Description;
        booking.StartTime      = request.StartTime.Value;
        booking.EndTime        = request.EndTime.Value;
        booking.Type           = request.Type;
        booking.OrganizerEmail = request.OrganizerEmail;
        booking.RoomId         = request.RoomId;

        await bookingRepository.UpdateAsync(booking);

        return (await bookingRepository.GetByIdAsync(id))!.ToSummary();
    }

    public async Task DeleteAsync(Guid id)
    {
        var booking = await bookingRepository.GetEntityByIdAsync(id)
            ?? throw new BookingNotFoundException(id);

        await bookingRepository.DeleteAsync(booking);
    }
}
