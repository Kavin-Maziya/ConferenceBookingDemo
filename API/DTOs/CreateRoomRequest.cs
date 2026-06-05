using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public record CreateRoomRequest(
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    string Name,

    [Required(ErrorMessage = "Floor is required")]
    [MaxLength(100, ErrorMessage = "Floor cannot exceed 100 characters")]
    string Floor,

    [Required(ErrorMessage = "Capacity is required")]
[Range(1, 1000, ErrorMessage = "Capacity must be between 1 and 1000")]
    int Capacity
    
);
