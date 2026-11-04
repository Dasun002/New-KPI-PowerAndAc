/*
 * File: EmailsController.cs
 * Handles CRUD operations for system email recipients used for notifications.
 */

using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/emails")]
    [Authorize]
    public class EmailsController : ControllerBase
    {
        // Database context for accessing email recipient records
        private readonly AppDbContext _db;

        public EmailsController(AppDbContext db)
        {
            _db = db;
        }

        // =========================================================
        // GET ALL EMAIL RECIPIENTS
        // =========================================================

        // GET: /api/emails/recipients
        [HttpGet("recipients")]
        public async Task<ActionResult<IEnumerable<object>>> GetRecipients()
        {
            // Fetch all recipients ordered by email
            var list = await _db.EmailRecipients
                .AsNoTracking()
                .OrderBy(x => x.Email)
                .Select(x => new { id = x.Id, email = x.Email })
                .ToListAsync();

            return Ok(list);
        }

        // =========================================================
        // ADD EMAIL RECIPIENT
        // =========================================================

        // POST: /api/emails/add-recipient
        [HttpPost("add-recipient")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult> AddRecipient([FromBody] EmailRecipientDto dto)
        {
            // Validate request body
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Normalize email input
            var email = (dto.Email ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email is required." });

            // Check if email already exists
            var exists = await _db.EmailRecipients.AnyAsync(x => x.Email.ToLower() == email);

            if (exists)
                return Conflict(new { message = "Email already exists." });

            // Create new recipient entity
            var entity = new EmailRecipient
            {
                Email = email,
                V = 0
            };

            _db.EmailRecipients.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new { id = entity.Id, email = entity.Email });
        }

        // =========================================================
        // UPDATE EMAIL RECIPIENT
        // =========================================================

        // PUT: /api/emails/update-recipient/{id}
        [HttpPut("update-recipient/{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult> UpdateRecipient(int id, [FromBody] EmailRecipientDto dto)
        {
            // Validate request body
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (id <= 0)
                return BadRequest(new { message = "Invalid recipient id." });

            // Find existing recipient
            var entity = await _db.EmailRecipients.FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
                return NotFound(new { message = "Recipient not found." });

            // Normalize email input
            var email = (dto.Email ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email is required." });

            // Ensure another record doesn't already use this email
            var existsOther = await _db.EmailRecipients
                .AnyAsync(x => x.Id != id && x.Email.ToLower() == email);

            if (existsOther)
                return Conflict(new { message = "Email already exists." });

            // Update email
            entity.Email = email;

            // Increment version safely (byte overflow protection)
            entity.V = (byte)Math.Min(255, entity.V + 1);

            await _db.SaveChangesAsync();

            return Ok(new { id = entity.Id, email = entity.Email });
        }

        // =========================================================
        // DELETE EMAIL RECIPIENT
        // =========================================================

        // DELETE: /api/emails/delete-recipient/{id}
        [HttpDelete("delete-recipient/{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteRecipient(int id)
        {
            if (id <= 0)
                return BadRequest(new { message = "Invalid recipient for deletion." });

            // Find recipient to delete
            var entity = await _db.EmailRecipients.FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
                return NotFound(new { message = "Recipient not found." });

            // Remove recipient
            _db.EmailRecipients.Remove(entity);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}