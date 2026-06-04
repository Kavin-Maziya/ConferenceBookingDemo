namespace API.DTOs;

// Extension methods that convert between DTO shapes.
// Centralising these mappings means the conversion logic lives in one place —
// if BookingDetailResponse or BookingResponse changes, only this file needs updating.
public static class MappingExtensions
{
    // Converts a full detail response into the lighter summary shape used by list endpoints.
    // Called by BookingService after a create or update to return a consistent response type.
    public static BookingResponse ToSummary(this BookingDetailResponse detail) =>
        new BookingResponse(
            detail.Id,
            detail.Title,
            detail.Type,
            detail.RoomName,
            detail.Floor,
            detail.StartTime,
            detail.EndTime,
            detail.OrganizerEmail,
            detail.Attendees.Count,
            detail.Attendees
                .Where(a => a.IsExternal)
                .Select(a => a.Name)
                .ToList()
        );
}
