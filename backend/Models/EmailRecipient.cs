/*
 * File: EmailRecipient.cs
 * Entity model representing email recipients.
 * Maps to the EmailRecipient table in the database.
 */

namespace backend.Models
{
    // =========================================================
    // EMAIL RECIPIENT MODEL
    // Represents an email recipient configuration
    // =========================================================
    public class EmailRecipient
    {
        // Unique identifier for the email recipient record
        public int Id { get; set; }

        // Email address of the recipient
        public string Email { get; set; } = string.Empty;

        // Version or status field
        public byte V { get; set; }
    }
}
