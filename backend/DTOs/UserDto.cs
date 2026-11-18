/*
 * File: UserDto.cs
 * Data Transfer Object for transferring complete user information.
 */

namespace backend.DTOs
{
    // =========================================================
    // USER DTO
    // Used for returning complete user details from the API
    // =========================================================
    public class UserDto
    {
        // Unique identifier of the user
        public int UserId { get; set; }

        // Service ID of the user
        public string ServiceId { get; set; } = string.Empty;

        // Full name of the user
        public string Name { get; set; } = string.Empty;

        // Role name assigned to the user
        public string Role { get; set; } = string.Empty;

        // Indicates whether the user account is active
        public bool IsActive { get; set; }

        // List of page names the user has access to
        public List<string> Pages { get; set; } = new();

        // List of page IDs the user has access to
        public List<int> PageIds { get; set; } = new();

        // Timestamp of the user's last login (optional)
        public DateTime? LastLogin { get; set; }

        // Timestamp when the user account was created
        public DateTime CreatedAt { get; set; }

        // Timestamp when the user account was last updated (optional)
        public DateTime? UpdatedAt { get; set; }
    }
}
