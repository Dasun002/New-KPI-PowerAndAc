/*
 * File: AdminUserDto.cs
 * Data Transfer Object used to represent administrator user information
 * when sending data between the API and frontend.
 */

namespace backend.DTOs
{
    // =========================================================
    // ADMIN USER DATA TRANSFER OBJECT
    // Used for returning admin user details to the frontend
    // =========================================================
    public class AdminUserDto
    {
        // Unique identifier of the user
        public string Id { get; set; } = "";

        // Full name of the user
        public string Name { get; set; } = "";

        // Service number used by the frontend (mapped from ServiceId)
        public string ServiceNumber { get; set; } = "";

        // Role assigned to the user (default: admin)
        public string Role { get; set; } = "admin";

        // Indicates whether the user account is active
        public bool IsActive { get; set; } = true;

        // Account creation timestamp
        public string? CreatedAt { get; set; }

        // Last login date and time
        public DateTime? LastLogin { get; set; }

        // List of pages the user has access to
        public List<string> Pages { get; set; } = new();
    }
}