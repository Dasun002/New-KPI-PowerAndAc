/*
 * File: PowerAcConfigController.cs
 * Admin configuration of the Power & A/C KPIs. The KPIs are KpiDefinition rows (Category = PowerAndAC), the same
 * table the other KPIs use. Name = KPI, Category = PowerAndAC, Target = DescriptionOfKPI + Unit,
 * Weightage = weight %, StrategicObjectives = KPI code (PM, SAFETY, CORRECTIVE, AVAILABILITY, or a new code).
 */
using backend.Data;
using backend.Models;
using backend.Services.PowerAc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/power-ac/config")]
    [Authorize(Policy = "AdminOnly")]
    public class PowerAcConfigController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PowerAcConfigController(AppDbContext db)
        {
            _db = db;
        }

        public class KpiDto
        {
            public string Name { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string Target { get; set; } = string.Empty;
            public string Unit { get; set; } = "%";
            public decimal Weightage { get; set; }
            // Whether the KPI is part of the scored set. New KPIs are included by default.
            public bool Included { get; set; } = true;
        }

        public class KpiRowDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Target { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public decimal Weightage { get; set; }
            public bool Included { get; set; }
        }

        [HttpGet("kpis")]
        public async Task<ActionResult<List<KpiRowDto>>> List()
        {
            var rows = await _db.KpiDefinitions.AsNoTracking()
                .Where(k => k.Category == PowerAcConfig.Category)
                .OrderBy(k => k.Id)
                .ToListAsync();
            return Ok(rows.Select(ToDto).ToList());
        }

        [HttpPost("kpis")]
        public async Task<ActionResult<KpiRowDto>> Add([FromBody] KpiDto dto)
        {
            var validation = Validate(dto);
            if (validation != null) return BadRequest(new { message = validation });
            var code = dto.Code.Trim().ToUpperInvariant();
            if (await _db.KpiDefinitions.AnyAsync(k => k.Category == PowerAcConfig.Category && k.StrategicObjectives == code))
                return Conflict(new { message = $"A Power & A/C KPI with code {code} already exists." });

            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var row = new KpiDefinition
            {
                Perspectives = "Customer",
                StrategicObjectives = code,
                KeyPerformanceIndicators = dto.Name.Trim(),
                Unit = dto.Unit.Trim(),
                DescriptionOfKPI = dto.Target.Trim(),
                Weightage = dto.Weightage,
                IncludeInScoring = dto.Included,
                PointsApplicable = 0,
                Category = PowerAcConfig.Category,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.KpiDefinitions.Add(row);
            await _db.SaveChangesAsync();
            return Ok(ToDto(row));
        }

        [HttpPut("kpis/{id:int}")]
        public async Task<ActionResult<KpiRowDto>> Update(int id, [FromBody] KpiDto dto)
        {
            var validation = Validate(dto);
            if (validation != null) return BadRequest(new { message = validation });
            var row = await _db.KpiDefinitions.FirstOrDefaultAsync(k => k.Id == id && k.Category == PowerAcConfig.Category);
            if (row is null) return NotFound(new { message = "KPI not found." });
            var code = dto.Code.Trim().ToUpperInvariant();
            if (await _db.KpiDefinitions.AnyAsync(k => k.Id != id && k.Category == PowerAcConfig.Category && k.StrategicObjectives == code))
                return Conflict(new { message = $"A Power & A/C KPI with code {code} already exists." });

            row.StrategicObjectives = code;
            row.KeyPerformanceIndicators = dto.Name.Trim();
            row.Unit = dto.Unit.Trim();
            row.DescriptionOfKPI = dto.Target.Trim();
            row.Weightage = dto.Weightage;
            row.IncludeInScoring = dto.Included;
            row.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            await _db.SaveChangesAsync();
            return Ok(ToDto(row));
        }

        [HttpDelete("kpis/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var row = await _db.KpiDefinitions.FirstOrDefaultAsync(k => k.Id == id && k.Category == PowerAcConfig.Category);
            if (row is null) return NotFound(new { message = "KPI not found." });
            _db.KpiDefinitions.Remove(row);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // A code is an upper-case identifier. A new KPI without a data column is stored, and shows as Incomplete in results.
        private static readonly System.Text.RegularExpressions.Regex CodePattern =
            new(@"^[A-Z][A-Z0-9_]{0,29}$");

        private static string? Validate(KpiDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) return "KPI name is required.";
            var code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
            if (!CodePattern.IsMatch(code))
                return "KPI code must start with a letter and use only letters, digits and underscores (max 30).";
            if (string.IsNullOrWhiteSpace(dto.Target)) return "Target is required.";
            if (dto.Weightage < 0m || dto.Weightage > 100m) return "Weightage must be between 0 and 100.";
            return null;
        }

        private static KpiRowDto ToDto(KpiDefinition k) => new()
        {
            Id = k.Id,
            Name = k.KeyPerformanceIndicators,
            Code = k.StrategicObjectives,
            Category = k.Category ?? string.Empty,
            Target = k.DescriptionOfKPI,
            Unit = k.Unit,
            Weightage = k.Weightage,
            Included = k.IncludeInScoring
        };
    }
}
