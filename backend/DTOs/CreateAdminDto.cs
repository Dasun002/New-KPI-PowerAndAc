/*
 * File: CreateAdminDto.cs
 * Data Transfer Object for creating new administrator users.
 */

namespace backend.DTOs
{
    // =========================================================
    // CREATE ADMIN DTO
    // Used for accepting admin user creation requests from frontend
    // =========================================================
    public class CreateAdminDto
    {
        // Full name of the user
        public string Name { get; set; } = "";

        // Service number as a 6-digit string (provided by frontend)
        public string ServiceNumber { get; set; } = "";

        // Role assigned to the user (default: admin)
        public string Role { get; set; } = "admin";

        // List of pages the user has access to (optional)
        public List<string>? Pages { get; set; }
    }
}
