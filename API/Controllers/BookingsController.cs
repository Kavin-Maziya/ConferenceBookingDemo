using Microsoft.AspNetCore.Mvc;
using API.Models;
using API.Data;
using API.DTOs;
using API.Exceptions;
using Microsoft.AspNetCore.Authorization;

namespace API.Controllers;

// Day 4 — Role definitions for this system:
//   Employee         — regular staff: can view and create bookings
//   Receptionist     — front desk:    can view, create, and update bookings
//   FacilitiesManager — manages rooms: can view and update bookings
//   Admin            — full access:   can do everything including delete

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    // ── PATTERN A: IActionResult ──────────────────────────────────────
    // Flexible — can return any response type.
    // PROBLEM: OpenAPI cannot infer what data shape is returned.
    // The generated documentation will show an unknown response body.
    [HttpGet("v-iactionresult")]
    public async Task<IActionResult> GetBookings_Untyped()
    {
        await Task.Delay(100);
        return Ok(BookingStore.Bookings); // ← OpenAPI: response body is "any"
    }

    // ── PATTERN B: ActionResult<T> ────────────────────────────────────
    // Typed — wraps both the T and allows returning IActionResult results.
    // OpenAPI knows the exact shape of the success response.
    // This is the correct pattern for all endpoints in this course.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Booking>>> GetBookingsAsync()
    {

        await Task.Delay(200); // stands in for: await _db.Bookings.ToListAsync()
        return Ok(BookingStore.Bookings); // HTTP 200 OK — Body: JSON array of Booking objects
    }

    // GET: /api/bookings/{id}
    // The ":guid" constraint means ASP.NET Core will ONLY match this route
    // if the {id} segment is a valid GUID format.
    // /api/bookings/abc         → 400 Bad Request (rejected by framework, never hits our code)
    // /api/bookings/00000000-...→ Hits our code, we handle 404 if not found
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Booking>> GetBookingByIdAsync(Guid id)
    {
        await Task.Delay(50);

        var booking = BookingStore.Bookings.FirstOrDefault(b => b.Id == id);

        // Guard clause: if the resource does not exist, say so explicitly.
        // Never return null. Never return a 200 with an empty body.
        // A 404 tells the client "this thing does not exist" — that is useful information.
        if (booking is null)
        {
            throw new BookingNotFoundException(id);
        }

        return Ok(booking); // HTTP 200 OK — Body: single Booking object as JSON
    }

    // POST: /api/bookings
    // Day 4 — Employees, Receptionists, and Admins can create bookings.
    // Facilities Managers only manage existing room assignments — they do not create new ones.
    [Authorize(Roles = "Employee,Receptionist,Admin")]
    [HttpPost]
    public async Task<ActionResult<BookingResponse>> CreateBookingAsync(
        [FromBody] CreateBookingRequest request)
    {
        await Task.Delay(50); // Simulate I/O latency — Week 2 replaces with real DB call

        // 1. IDEMPOTENCY GUARD
        // Prevent a duplicate if the client submits the same form twice.
        // (React Strict Mode fires effects twice; users double-click Submit buttons.)
        bool isDuplicate = BookingStore.Bookings.Any(b =>
            b.Room == request.Room && b.StartTime == request.StartTime);

        if (isDuplicate)
        {
           throw new DuplicateBookingException(request.Room, request.StartTime!.Value); // HTTP 409 — Problem Details middleware fills in the body
        }

        // 2. MAP DTO → DOMAIN MODEL
        // Server generates the ID — the client never controls this.
        var newBooking = new Booking(
            Guid.NewGuid(),
            request.Title,
            request.Speaker,
            request.Room,
            request.StartTime!.Value // DateTime? from DTO — safe to unwrap, [Required] already validated
        );

        // 3. SAVE
        BookingStore.Bookings.Add(newBooking);

        // 4. MAP DOMAIN MODEL → RESPONSE DTO
        var response = new BookingResponse(
            newBooking.Id,
            newBooking.Title,
            newBooking.Speaker,
            newBooking.Room,
            newBooking.StartTime
        );

        // 5. RETURN 201 CREATED + Location header
        // CreatedAtAction sets the Location header to: GET /api/bookings/{id}
        return CreatedAtAction(nameof(GetBookingByIdAsync), new { id = response.id }, response);
    }

    // PUT: /api/bookings/{id}
    // Replaces all fields of an existing booking.
    // Day 4 — Receptionists update on behalf of attendees.
    //          Facilities Managers can reassign or adjust room details.
    //          Admins have full access. Employees must contact the front desk to modify.
    [Authorize(Roles = "Receptionist,FacilitiesManager,Admin")]
    [HttpPut("{id:guid}")]
    
    public async Task<ActionResult<BookingResponse>> UpdateBookingAsync(
        Guid id,
        [FromBody] CreateBookingRequest request)
    {
        await Task.Delay(50);

        var existingBooking = BookingStore.Bookings.FirstOrDefault(b => b.Id == id);

        if (existingBooking is null)
        {
            return NotFound();
        }

        // Records are immutable — you cannot mutate properties directly.
        // The 'with' expression creates a new copy with the changed fields only.
        var updatedBooking = existingBooking with
        {
            Title     = request.Title,
            Speaker   = request.Speaker,
            Room      = request.Room,
            StartTime = request.StartTime!.Value // nullable because of [Required] on DTO
        };

        BookingStore.Bookings.Remove(existingBooking);
        BookingStore.Bookings.Add(updatedBooking);

        var response = new BookingResponse(
            updatedBooking.Id,
            updatedBooking.Title,
            updatedBooking.Speaker,
            updatedBooking.Room,
            updatedBooking.StartTime
        );

        return Ok(response); // HTTP 200 OK — returns the updated booking
    }

    // DELETE: /api/bookings/{id}
    // Day 4 — Admin only. Deletion is irreversible; no other role can perform it.
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
   
    public async Task<ActionResult> DeleteBookingAsync(Guid id)
    {
        await Task.Delay(50);

        var booking = BookingStore.Bookings.FirstOrDefault(b => b.Id == id);

        if (booking is null)
        {
           throw new BookingNotFoundException(id); // HTTP 404 — Problem Details middleware adds the body
        }

        BookingStore.Bookings.Remove(booking);

        return NoContent(); // HTTP 204 — operation succeeded, nothing to return
    }
}
