/*
 * File: EmailRecipientDto.cs
 * Data Transfer Object for email recipient information.
 */

using System.ComponentModel.DataAnnotations;

namespace backend.DTOs
{
    // =========================================================
    // EMAIL RECIPIENT DTO
    // Used for specifying email recipients
    // =========================================================
    public class EmailRecipientDto
    {
        // Email address of the recipient (required and must be valid)
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
