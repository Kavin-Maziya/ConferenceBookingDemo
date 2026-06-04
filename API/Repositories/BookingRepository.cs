using API.Data;
using API.DTOs;
using Microsoft.EntityFrameworkCore;
using API.Models;

namespace API.Repositories;

public class BookingRepository(BookingDbContext db) : IBookingRepository
{
    public async Task<IEnumerable<BookingResponse>> GetAllAsync() =>
     await db.Bookings
         .AsNoTracking()
         .Select(b => new BookingResponse(
             b.Id,
             b.Title,
             b.Type.ToString(),
             b.Room.Name,
             b.Room.Floor,
             b.StartTime,
             b.EndTime,
             b.OrganizerEmail,
             b.Attendees.Count,
             b.Attendees
                 .Where(ba => ba.Attendee.IsExternal)
                 .Select(ba => ba.Attendee.Name)
                 .ToList()))
         .ToListAsync();

    public async Task<BookingDetailResponse?> GetByIdAsync(Guid id)
    {
        var booking = await db.Bookings
            .AsNoTracking()
            .Include(b => b.Room)
                .ThenInclude(r => r.Equipment)
                    .ThenInclude(re => re.Equipment)
            .Include(b => b.Attendees)
                .ThenInclude(ba => ba.Attendee)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null) return null;

        return new BookingDetailResponse(
            booking.Id,
            booking.Title,
            booking.Description,
            booking.Type.ToString(),
            booking.Room.Name,
            booking.Room.Floor,
            booking.Room.Capacity,
            booking.StartTime,
            booking.EndTime,
            booking.OrganizerEmail,
            booking.Room.Equipment.Select(re => new RoomEquipmentResponse(
                re.Equipment.Name,
                re.Equipment.Description,
                re.Quantity)).ToList(),
            booking.Attendees.Select(ba => new AttendeeResponse(
                ba.Attendee.Name,
                ba.Attendee.Email,
                ba.Attendee.IsExternal,
                ba.InvitedAt)).ToList()
        );
    }

    // FindAsync checks the Change Tracker before hitting the database.
    // No AsNoTracking — the caller (service) needs a tracked entity to mutate and save.
    public async Task<Booking?> GetEntityByIdAsync(Guid id) =>
        await db.Bookings.FindAsync(id);

    public async Task<bool> HasConflictAsync(Guid roomId, DateTime start, DateTime end, Guid? excludeBookingId = null) =>
      await db.Bookings.AnyAsync(b =>
          b.RoomId == roomId &&
          b.Id != excludeBookingId &&
          b.StartTime < end &&
          b.EndTime > start);

    public async Task<IEnumerable<BookingResponse>>
    SearchAsync(BookingSearchQuery query)
    {
        IQueryable<Booking> q = db.Bookings
            .AsNoTracking()
            .Where(b => b.Room.IsAvailable);

        if (!string.IsNullOrWhiteSpace(query.RoomName))
            q = q.Where(b => b.Room.Name.Contains(query.RoomName));

        if (query.Type.HasValue)
            q = q.Where(b => b.Type == query.Type.Value);

        if (query.From.HasValue)
            q = q.Where(b => b.StartTime >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(b => b.EndTime <= query.To.Value);

        return await q
            .OrderBy(b => b.StartTime)
            .Select(b => new BookingResponse(
                b.Id, b.Title, b.Type.ToString(),
                b.Room.Name, b.Room.Floor,
                b.StartTime, b.EndTime, b.OrganizerEmail,
                b.Attendees.Count,
                b.Attendees
                    .Where(ba => ba.Attendee.IsExternal)
                    .Select(ba => ba.Attendee.Name)
                    .ToList()))
            .ToListAsync();
    }
    public async Task<Booking> AddAsync(Booking booking)
    {
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    public async Task<Booking> UpdateAsync(Booking booking)
    {
        db.Bookings.Update(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    public async Task<Booking> DeleteAsync(Booking booking)
    {
        db.Bookings.Remove(booking);
        await db.SaveChangesAsync();
        return booking;
    }
}

