/*
 * File: User.cs
 * Entity model representing application users with authentication and authorization data.
 * Maps to the Users table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // USER MODEL
    // Represents an application user with role and access information
    // =========================================================
    [Table("Users")]
    public class User
    {
        // Unique identifier for the user
        [Key]
        public int UserId { get; set; }

        // Service ID/Employee ID from Azure AD (required, max 20 characters)
        [Required]
        [MaxLength(20)]
        public string ServiceId { get; set; } = string.Empty;

        // Email address of the user (optional, max 150 characters)
        [MaxLength(150)]
        public string? Email { get; set; }

        // Full name of the user (required, max 150 characters)
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        // Foreign key to the user's role
        public int RoleId { get; set; }
        
        // Navigation property: reference to the user's role
        [ForeignKey("RoleId")]
        public Role? Role { get; set; }

        // Indicates whether the user account is active
        public bool IsActive { get; set; } = true;

        // Timestamp of the user's last login (optional)
        public DateTime? LastLogin { get; set; }

        // Timestamp when the user account was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Timestamp when the user account was last updated (optional)
        public DateTime? UpdatedAt { get; set; }
    }
}