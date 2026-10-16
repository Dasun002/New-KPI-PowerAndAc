/*
 * File: Role.cs
 * Entity model representing user roles for authorization.
 * Maps to the Roles table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // ROLE MODEL
    // Represents user roles for role-based access control
    // =========================================================
    [Table("Roles")]
    public class Role
    {
        // Unique identifier for the role
        [Key]
        public int RoleId { get; set; }
        
        // Role name (required, max 50 characters)
        [Required]
        [MaxLength(50)]
        public string RoleName { get; set; } = string.Empty;
        
        // Timestamp when the role was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
