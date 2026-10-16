/*
 * File: Page.cs
 * Entity model representing application pages that users can access.
 * Maps to the Page table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // PAGE MODEL
    // Represents an application page that can be assigned to users
    // =========================================================
    [Table("Page")]
    public class Page
    {
        // Unique identifier for the page
        [Key]
        public byte PageId { get; set; }

        // Page code identifier (required, max 50 characters)
        [Required]
        [MaxLength(50)]
        public string PageCode { get; set; } = string.Empty;

        // Page name or display label (required, max 100 characters)
        [Required]
        [MaxLength(100)]
        public string PageName { get; set; } = string.Empty;

        // Timestamp when the page was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
