/*
 * File: UserPageAccess.cs
 * Entity model representing page-level access assignments to users.
 * Maps to the UserPageAccess table in the database.
 */

using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // USER PAGE ACCESS MODEL
    // Represents which pages/modules a user can access
    // =========================================================
    [Table("UserPageAccess")]
    public class UserPageAccess
    {
        // Foreign key to the User who has access
        public int UserId { get; set; }
        
        // Navigation property: reference to User
        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Foreign key to the Page the user can access
        public byte PageId { get; set; }
        
        // Navigation property: reference to Page
        [ForeignKey("PageId")]
        public Page? Page { get; set; }
    }
}
