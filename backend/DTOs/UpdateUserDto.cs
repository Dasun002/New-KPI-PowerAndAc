/*
 * File: UpdateUserDto.cs
 * Data Transfer Object for updating existing user accounts.
 */

namespace backend.DTOs
{
    // =========================================================
    // UPDATE USER DTO
    // Used for accepting user update requests (partial updates)
    // =========================================================
    public class UpdateUserDto
    {
        // Service ID of the user (optional)
        public string? ServiceId { get; set; }

        // Full name of the user (optional)
        public string? Name { get; set; }

        // Role name assigned to the user (optional)
        public string? Role { get; set; }

        // Indicates whether the user account is active (optional)
        public bool? IsActive { get; set; }

        // List of page names the user has access to (legacy/optional)
        public List<string>? Pages { get; set; }

        // List of page IDs the user has access to (optional)
        public List<int>? PageIds { get; set; }
    }
}
