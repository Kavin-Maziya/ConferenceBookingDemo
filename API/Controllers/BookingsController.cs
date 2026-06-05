using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Data;
using API.DTOs;
using API.Exceptions;
using API.Models;
using API.Services;

namespace API.Controllers;

// Role definitions for this system:
//   Employee          — can view and create bookings
//   Receptionist      — can view, create, and update bookings (manages visitor registrations)
//   FacilitiesManager — can view, create, and update bookings (manages maintenance windows)
//   Admin             — full access, including delete

[ApiController]
[Route("api/[controller]")]
// BookingDbContext is injected via primary constructor (DI, Scoped lifetime — one per HTTP request).
public class BookingsController(IBookingService bookingService) : ControllerBase
{
    // ── IActionResult vs ActionResult<T> comparison (teaching reference) ──────
    // PATTERN A: IActionResult — flexible but OpenAPI cannot infer the response shape.
    [HttpGet("v-iactionresult")]
    public async Task<IActionResult> GetBookings_Untyped()
    {
        var bookings = await bookingService.GetAllAsync();
        return Ok(bookings); // OpenAPI shows response body as "any" — not helpful for client generation
    }

    // PATTERN B: ActionResult<T> — OpenAPI knows the exact response shape. Use this everywhere.

    // ── GET /api/bookings ─────────────────────────────────────────────────────
    // Returns a projected list of all bookings.
    // Anonymous — the conference schedule is public. No token required.
    //
    // Query strategy: SELECT projection + AsNoTracking.
    //   AsNoTracking skips the Change Tracker snapshot entirely — no writes follow a GET.
    //   Select projects only the columns the DTO needs, so Room.Capacity and Room.IsAvailable
    //   never cross the wire. AttendeeCount uses a COUNT(*) subquery, not a full join.
    //   This is the fastest EF Core read pattern for list endpoints.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingResponse>>> GetBookingsAsync()
    {
        var bookings = await bookingService.GetAllAsync();
        return Ok(bookings);
    }

    // ── GET /api/bookings/search ──────────────────────────────────────────────
    // Optional filters for the receptionist's schedule view.
    // IQueryable lets us compose the WHERE clauses conditionally — only applied filters
    // appear in the SQL. Contrast with loading all rows and filtering in C# (IEnumerable).
    [HttpGet("search")]
    // public async Task<ActionResult<IEnumerable<BookingResponse>>> SearchBookingsAsync(
    //     [FromQuery] string?      roomName,
    //     [FromQuery] BookingType? type,
    //     [FromQuery] DateTime?    from,
    //     [FromQuery] DateTime?    to)
    // {
    //     // Start with all bookings in available rooms — the base condition always applies.
    //     IQueryable<Booking> query = bookingService.SearchAsync(query)
    //         .AsNoTracking()
    //         .Where(b => b.Room.IsAvailable);

    //     // Each condition adds a SQL WHERE clause only when the parameter was supplied.
    //     // No parameter supplied → no WHERE clause → all rows included for that dimension.
    //     if (!string.IsNullOrWhiteSpace(roomName))
    //         query = query.Where(b => b.Room.Name.Contains(roomName));

    //     if (type.HasValue)
    //         query = query.Where(b => b.Type == type.Value);

    //     if (from.HasValue)
    //         query = query.Where(b => b.StartTime >= from.Value);

    //     if (to.HasValue)
    //         query = query.Where(b => b.EndTime <= to.Value);

    //     return Ok(await ProjectToResponse(query.OrderBy(b => b.StartTime)).ToListAsync());
    // }

    public async Task<ActionResult<IEnumerable<BookingResponse>>> SearchBookingsAsync(
        [FromQuery] BookingSearchQuery query)
    {
        var bookings = await bookingService.SearchAsync(query);
        return Ok(bookings);
    }

    // ── GET /api/bookings/{id} ────────────────────────────────────────────────
    // Returns full booking detail including room equipment and all attendees.
    //
    // Query strategy: Include + ThenInclude (eager loading).
    //   The detail endpoint needs the full object graph:
    //     Booking → Room → RoomEquipment → Equipment
    //     Booking → BookingAttendee → Attendee
    //   ThenInclude chains off a navigation already included — each level deeper
    //   requires one more ThenInclude call. EF Core generates JOIN clauses for each.
    [HttpGet("{id:guid}")]
    // public async Task<ActionResult<BookingDetailResponse>> GetBookingByIdAsync(Guid id)
    // {
    //     // Include + ThenInclude loads the full graph in a single SQL query with JOINs.
    //     // AsNoTracking is correct here — we are only reading, not mutating.
    //     var booking = await bookingService
    //         .AsNoTracking()
    //         .Include(b => b.Room)
    //             .ThenInclude(r => r.Equipment)      // Room → RoomEquipment rows
    //                 .ThenInclude(re => re.Equipment) // RoomEquipment → Equipment entity
    //         .Include(b => b.Attendees)              // Booking → BookingAttendee rows
    //             .ThenInclude(ba => ba.Attendee)     // BookingAttendee → Attendee entity
    //         .FirstOrDefaultAsync(b => b.Id == id);

    //     if (booking is null)
    //         throw new BookingNotFoundException(id); // 404 via GlobalExceptionHandler

    //     // Map to a detail DTO that exposes the full graph to the client.
    //     var response = new BookingDetailResponse(
    //         booking.Id,
    //         booking.Title,
    //         booking.Description,
    //         booking.Type.ToString(),
    //         booking.Room.Name,
    //         booking.Room.Floor,
    //         booking.Room.Capacity,
    //         booking.StartTime,
    //         booking.EndTime,
    //         booking.OrganizerEmail,
    //         booking.Room.Equipment.Select(re => new RoomEquipmentResponse(
    //             re.Equipment.Name,
    //             re.Equipment.Description,
    //             re.Quantity)).ToList(),
    //         booking.Attendees.Select(ba => new AttendeeResponse(
    //             ba.Attendee.Name,
    //             ba.Attendee.Email,
    //             ba.Attendee.IsExternal,
    //             ba.InvitedAt)).ToList()
    //     );

    //     return Ok(response);
    // }
    public async Task<ActionResult<BookingDetailResponse>> GetBookingByIdAsync(Guid id)
    {
        // BookingNotFoundException thrown by service → caught by GlobalExceptionHandler → 404
        var booking = await bookingService.GetByIdAsync(id);
        return Ok(booking);
    }

    // ── POST /api/bookings ────────────────────────────────────────────────────
    // Employees, Receptionists, FacilitiesManagers, and Admins can create bookings.
    [Authorize(Roles = "Employee,Receptionist,FacilitiesManager,Admin")]
    [HttpPost]
    // public async Task<ActionResult<BookingResponse>> CreateBookingAsync(
    //     [FromBody] CreateBookingRequest request)
    // {
    //     // Validate that the referenced room exists before checking for conflicts.
    //     var room = await db.Rooms.FindAsync(request.RoomId);
    //     if (room is null)
    //         return NotFound($"Room '{request.RoomId}' does not exist.");

    //     // Overlap detection: two bookings conflict if one starts before the other ends.
    //     // (StartA < EndB && EndA > StartB) is the standard interval overlap formula.
    //     // A simple unique index on (RoomId, StartTime) only catches exact start-time
    //     // duplicates — it cannot detect partial overlaps like 09:00–11:00 vs 10:00–12:00.
    //     bool hasConflict = await db.Bookings.AnyAsync(b =>
    //         b.RoomId    == request.RoomId &&
    //         b.StartTime <  request.EndTime!.Value &&
    //         b.EndTime   >  request.StartTime!.Value);

    //     if (hasConflict)
    //         throw new DuplicateBookingException(room.Name, request.StartTime!.Value, request.EndTime!.Value);

    //     var newBooking = new Booking
    //     {
    //         Id             = Guid.NewGuid(),
    //         Title          = request.Title,
    //         Description    = request.Description,
    //         StartTime      = request.StartTime!.Value,
    //         EndTime        = request.EndTime!.Value,
    //         Type           = request.Type,
    //         OrganizerEmail = request.OrganizerEmail,
    //         RoomId         = request.RoomId
    //     };

    //     // Add() stages the entity in the Change Tracker as Added.
    //     // SaveChangesAsync() executes: INSERT INTO bookings (...)
    //     db.Bookings.Add(newBooking);
    //     await db.SaveChangesAsync();

    //     // Return a projected response — the same shape as the list endpoint.
    //     var response = await ProjectToResponse(
    //         db.Bookings.AsNoTracking().Where(b => b.Id == newBooking.Id))
    //         .FirstAsync();

    //     // 201 Created + Location header pointing to GET /api/bookings/{id}
    //     return CreatedAtAction(nameof(GetBookingByIdAsync), new { id = response.Id }, response);
    // }
     public async Task<ActionResult<BookingResponse>> CreateBookingAsync(
        [FromBody] CreateBookingRequest request)
    {
        var booking = await bookingService.CreateAsync(request);
        return CreatedAtAction(nameof(GetBookingByIdAsync), new { id = booking.Id }, booking);
    }

    // ── PUT /api/bookings/{id} ────────────────────────────────────────────────
    // Receptionists update on behalf of attendees (room changes, time adjustments).
    // FacilitiesManagers reschedule maintenance windows.
    // Employees must contact reception to modify a booking.
    [Authorize(Roles = "Receptionist,FacilitiesManager,Admin")]
    [HttpPut("{id:guid}")]
    // public async Task<ActionResult<BookingResponse>> UpdateBookingAsync(
    //     Guid id,
    //     [FromBody] CreateBookingRequest request)
    // {
    //     // FindAsync checks the Change Tracker first, then the database.
    //     // The Change Tracker snapshots the entity so SaveChangesAsync can detect mutations.
    //     // NOTE: Do NOT use AsNoTracking here — we need the snapshot for change detection.
    //     var booking = await db.Bookings.FindAsync(id);

    //     if (booking is null)
    //         return NotFound();

    //     // Validate the new room exists if it changed.
    //     var room = await db.Rooms.FindAsync(request.RoomId);
    //     if (room is null)
    //         return NotFound($"Room '{request.RoomId}' does not exist.");

    //     // Check for conflicts, excluding this booking's own ID (a booking never conflicts with itself).
    //     bool hasConflict = await db.Bookings.AnyAsync(b =>
    //         b.Id        != id &&
    //         b.RoomId    == request.RoomId &&
    //         b.StartTime <  request.EndTime!.Value &&
    //         b.EndTime   >  request.StartTime!.Value);

    //     if (hasConflict)
    //         throw new DuplicateBookingException(room.Name, request.StartTime!.Value, request.EndTime!.Value);

    //     // Mutate properties directly — the Change Tracker detects these changes by
    //     // comparing the current state against the snapshot taken at FindAsync time.
    //     booking.Title          = request.Title;
    //     booking.Description    = request.Description;
    //     booking.StartTime      = request.StartTime!.Value;
    //     booking.EndTime        = request.EndTime!.Value;
    //     booking.Type           = request.Type;
    //     booking.OrganizerEmail = request.OrganizerEmail;
    //     booking.RoomId         = request.RoomId;

    //     // SaveChangesAsync generates: UPDATE bookings SET ... WHERE id = @id
    //     await db.SaveChangesAsync();

    //     var response = await ProjectToResponse(
    //         db.Bookings.AsNoTracking().Where(b => b.Id == id))
    //         .FirstAsync();

    //     return Ok(response);
    // }
    public async Task<ActionResult<BookingResponse>> UpdateBookingAsync(
        Guid id,
        [FromBody] CreateBookingRequest request)
    {
        var booking = await bookingService.UpdateAsync(id, request);
        return Ok(booking);
    }

    // ── DELETE /api/bookings/{id} ─────────────────────────────────────────────
    // Admin only — deletion is irreversible. All other roles must contact an Admin.
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    // public async Task<ActionResult> DeleteBookingAsync(Guid id)
    // {
    //     var booking = await db.Bookings.FindAsync(id);

    //     if (booking is null)
    //         throw new BookingNotFoundException(id); // 404 via GlobalExceptionHandler

    //     // Remove() marks the entity as Deleted in the Change Tracker (synchronous).
    //     // SaveChangesAsync() executes: DELETE FROM bookings WHERE id = @id
    //     db.Bookings.Remove(booking);
    //     await db.SaveChangesAsync();

    //     return NoContent(); // 204 — operation succeeded, no body returned
    // }

    public async Task<ActionResult> DeleteBookingAsync(Guid id)
    {
        await bookingService.DeleteAsync(id);
        return NoContent(); // 204
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    // ProjectToResponse converts an IQueryable<Booking> into an IQueryable<BookingResponse>
    // using a Select projection. EF Core translates the entire lambda into SQL, so only
    // the columns needed by the DTO travel from the database to the application.
    //
    // Key points:
    //   - b.Room.Name accesses the joined rooms table — EF Core generates a JOIN automatically.
    //   - b.Attendees.Count() translates to a COUNT(*) subquery — no attendee rows are loaded.
    //   - The .Where(ba => ba.Attendee.IsExternal) filter runs in PostgreSQL, not in C#.
    private static IQueryable<BookingResponse> ProjectToResponse(IQueryable<Booking> query) =>
        query.Select(b => new BookingResponse(
            b.Id,
            b.Title,
            b.Type.ToString(),
            b.Room.Name,     // EF Core JOINs rooms — only the Name column is selected
            b.Room.Floor,
            b.StartTime,
            b.EndTime,
            b.OrganizerEmail,
            b.Attendees.Count,   // COUNT(*) subquery — avoids loading attendee rows
            b.Attendees          // Filter external visitors inline in the database
                .Where(ba => ba.Attendee.IsExternal)
                .Select(ba => ba.Attendee.Name)
                .ToList()
        ));
}
