/*
 * File: PlatformKpiAssignment.cs
 * Entity model representing Platform KPI page assignments to users.
 * Maps to the PlatformKpiAssignment table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // PLATFORM KPI ASSIGNMENT MODEL
    // Represents assignment of Platform KPI editing pages to users
    // =========================================================
    [Table("PlatformKpiAssignment")]
    public class PlatformKpiAssignment
    {
        // Unique identifier for the assignment
        [Key]
        public int AssignmentId { get; set; }

        // Foreign key to the User who is assigned
        public int UserId { get; set; }
        
        // Navigation property: reference to User
        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Foreign key to the Page assigned for KPI editing
        public byte PageId { get; set; }
        
        // Navigation property: reference to Page
        [ForeignKey("PageId")]
        public Page? Page { get; set; }

        // Timestamp when the assignment was created
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
