/*
 * File: PowerAcDataController.cs
 * Engineer data entry for the Power & AC KPI: effective-dated area allocations and monthly
 * component data (Critical Safety, Corrective Action, Power System Availability), with a
 * Draft -> Submitted -> Verified/Rejected workflow.
 * Preventive Maintenance data continues to use the existing api/PowerAndAC endpoints.
 */
using backend.Data;
using backend.DTOs.PowerAc;
using backend.Models.PowerAc;
using backend.Services.PowerAc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/power-ac")]
    [Authorize]
    public class PowerAcDataController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly PowerAcAccessService _access;

        public PowerAcDataController(AppDbContext db, PowerAcAccessService access)
        {
            _db = db;
            _access = access;
        }

        // ---------------- Allocations ----------------

        // Effective allocation of every engineer group for a month, with the total ('Power & AC'!D40).
        [HttpGet("allocations")]
        public async Task<ActionResult<PowerAcAllocationSummaryDto>> GetAllocations([FromQuery] int? year, [FromQuery] int? month)
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            var now = DateTime.UtcNow;
            var y = year ?? now.Year;
            var m = month ?? now.Month;
            if (m < 1 || m > 12) return BadRequest(new { message = "month must be between 1 and 12." });

            var manageable = await _access.GetManageableGroupIdsAsync(User);
            var groups = await _db.PowerAcEngineerGroups.AsNoTracking().Include(g => g.RegionData)
                .Where(g => g.IsActive).OrderBy(g => g.SortOrder).ThenBy(g => g.Id).ToListAsync();
            var key = PowerAcPeriod.Key(y, m);
            var allocations = (await _db.PowerAcAreaAllocations.AsNoTracking().ToListAsync())
                .Where(a => PowerAcPeriod.Key(a.EffectiveYear, a.EffectiveMonth) <= key)
                .GroupBy(a => a.EngineerGroupId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => PowerAcPeriod.Key(a.EffectiveYear, a.EffectiveMonth)).First());

            var items = groups.Select(g =>
            {
                allocations.TryGetValue(g.Id, out var a);
                return new PowerAcAllocationDto
                {
                    EngineerGroupId = g.Id,
                    EngineerGroupCode = g.Code,
                    WorkbookLabel = g.WorkbookLabel,
                    WorkbookAllocationCell = g.WorkbookAllocationCell,
                    NetworkEngineer = g.RegionData?.NetworkEngineer,
                    LeaCode = g.RegionData?.LeaCode,
                    MappingVerified = g.MappingVerified,
                    AllocationId = a?.Id,
                    AllocationValue = a?.AllocationValue,
                    EffectiveYear = a?.EffectiveYear,
                    EffectiveMonth = a?.EffectiveMonth,
                    Remarks = a?.Remarks,
                    UpdatedAt = a?.UpdatedAt ?? a?.CreatedAt,
                    UpdatedBy = a?.UpdatedBy ?? a?.CreatedBy,
                    CanEdit = manageable.Contains(g.Id)
                };
            }).ToList();

            var missing = items.Count(i => i.AllocationValue is null);
            var total = items.Sum(i => i.AllocationValue ?? 0m);
            return Ok(new PowerAcAllocationSummaryDto
            {
                Year = y,
                Month = m,
                Items = items,
                Total = total,
                MissingCount = missing,
                IsComplete = missing == 0 && total > 0m
            });
        }

        [HttpGet("allocations/history")]
        public async Task<ActionResult<List<PowerAcAllocationDto>>> GetAllocationHistory([FromQuery] int engineerGroupId)
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            var group = await _db.PowerAcEngineerGroups.AsNoTracking().Include(g => g.RegionData).FirstOrDefaultAsync(g => g.Id == engineerGroupId);
            if (group is null) return NotFound(new { message = "Engineer group not found." });

            var rows = await _db.PowerAcAreaAllocations.AsNoTracking()
                .Where(a => a.EngineerGroupId == engineerGroupId)
                .OrderByDescending(a => a.EffectiveYear).ThenByDescending(a => a.EffectiveMonth)
                .ToListAsync();
            return Ok(rows.Select(a => new PowerAcAllocationDto
            {
                EngineerGroupId = group.Id,
                EngineerGroupCode = group.Code,
                WorkbookLabel = group.WorkbookLabel,
                WorkbookAllocationCell = group.WorkbookAllocationCell,
                NetworkEngineer = group.RegionData?.NetworkEngineer,
                LeaCode = group.RegionData?.LeaCode,
                MappingVerified = group.MappingVerified,
                AllocationId = a.Id,
                AllocationValue = a.AllocationValue,
                EffectiveYear = a.EffectiveYear,
                EffectiveMonth = a.EffectiveMonth,
                Remarks = a.Remarks,
                UpdatedAt = a.UpdatedAt ?? a.CreatedAt,
                UpdatedBy = a.UpdatedBy ?? a.CreatedBy
            }).ToList());
        }

        // Saves allocation values effective from the given month. Earlier months keep their own values.
        [HttpPut("allocations")]
        public async Task<IActionResult> SaveAllocations([FromBody] SavePowerAcAllocationsDto dto)
        {
            if (dto.Items.GroupBy(i => i.EngineerGroupId).Any(g => g.Count() > 1))
                return BadRequest(new { message = "Each engineer group may appear only once." });

            var manageable = await _access.GetManageableGroupIdsAsync(User);
            var groupIds = dto.Items.Select(i => i.EngineerGroupId).ToList();
            var known = await _db.PowerAcEngineerGroups.Where(g => groupIds.Contains(g.Id)).Select(g => g.Id).ToListAsync();
            var unknown = groupIds.Except(known).ToList();
            if (unknown.Count > 0) return BadRequest(new { message = $"Unknown engineer groups: {string.Join(", ", unknown)}." });

            var forbidden = groupIds.Where(id => !manageable.Contains(id)).ToList();
            if (forbidden.Count > 0) return Forbid();

            if (dto.Items.Any(i => decimal.Round(i.AllocationValue, 4) != i.AllocationValue))
                return BadRequest(new { message = "Allocation values may have at most 4 decimal places." });

            if (await PowerAcRules.HasCalculatedResultsFromAsync(_db, dto.EffectiveYear, dto.EffectiveMonth))
                return Conflict(new { message = "Results have already been calculated for this effective month or later. Enter the allocation from a later effective month so earlier results stay reproducible." });

            var user = PowerAcAccessService.CurrentUserName(User);
            var now = DateTime.UtcNow;
            var existing = await _db.PowerAcAreaAllocations
                .Where(a => groupIds.Contains(a.EngineerGroupId) && a.EffectiveYear == dto.EffectiveYear && a.EffectiveMonth == dto.EffectiveMonth)
                .ToListAsync();

            var changes = new List<(PowerAcAreaAllocation Row, bool IsNew, decimal? OldValue)>();
            foreach (var item in dto.Items)
            {
                var row = existing.FirstOrDefault(a => a.EngineerGroupId == item.EngineerGroupId);
                if (row is null)
                {
                    row = new PowerAcAreaAllocation
                    {
                        EngineerGroupId = item.EngineerGroupId,
                        EffectiveYear = (short)dto.EffectiveYear,
                        EffectiveMonth = (byte)dto.EffectiveMonth,
                        AllocationValue = item.AllocationValue,
                        Remarks = item.Remarks?.Trim(),
                        CreatedAt = now,
                        CreatedBy = user
                    };
                    _db.PowerAcAreaAllocations.Add(row);
                    changes.Add((row, true, null));
                }
                else
                {
                    changes.Add((row, false, row.AllocationValue));
                    row.AllocationValue = item.AllocationValue;
                    row.Remarks = item.Remarks?.Trim();
                    row.UpdatedAt = now;
                    row.UpdatedBy = user;
                }
            }

            await _db.SaveChangesAsync();
            foreach (var (row, isNew, oldValue) in changes)
            {
                PowerAcAudit.Add(_db, PowerAcAuditEntities.AreaAllocation, row.Id,
                    isNew ? PowerAcAuditActions.Created : PowerAcAuditActions.Updated, null, null, user,
                    new { row.EngineerGroupId, row.EffectiveYear, row.EffectiveMonth, oldValue, newValue = row.AllocationValue, row.Remarks });
            }
            await _db.SaveChangesAsync();
            return Ok(new { message = "Allocations saved.", saved = dto.Items.Count });
        }

        // ---------------- Component data ----------------

        [HttpGet("component-results")]
        public async Task<ActionResult<List<PowerAcComponentResultDto>>> GetComponentResults(
            [FromQuery] int year, [FromQuery] int? month, [FromQuery] int? componentId,
            [FromQuery] int? engineerGroupId, [FromQuery] string? status)
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            var manageable = await _access.GetManageableGroupIdsAsync(User);
            var isAdmin = PowerAcAccessService.IsAdmin(User);

            var q = _db.PowerAcComponentResults.AsNoTracking()
                .Include(r => r.EngineerGroup).Include(r => r.Component)
                .Where(r => r.Year == year);
            if (month.HasValue) q = q.Where(r => r.Month == month.Value);
            if (componentId.HasValue) q = q.Where(r => r.ComponentId == componentId.Value);
            if (engineerGroupId.HasValue) q = q.Where(r => r.EngineerGroupId == engineerGroupId.Value);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);

            var rows = await q.OrderBy(r => r.Month).ThenBy(r => r.EngineerGroup!.SortOrder).ThenBy(r => r.Component!.SortOrder).ToListAsync();
            return Ok(rows.Select(r => ToDto(r, CanEditRecord(r, manageable, isAdmin))).ToList());
        }

        // Creates or updates the record for (group, year, month, component). Any change returns it to Draft.
        [HttpPut("component-results")]
        public async Task<ActionResult<PowerAcComponentResultDto>> SaveComponentResult([FromBody] SavePowerAcComponentResultDto dto)
        {
            var component = await _db.PowerAcComponents.FirstOrDefaultAsync(c => c.Id == dto.ComponentId);
            if (component is null) return BadRequest(new { message = "Component not found." });
            if (component.DataSource != PowerAcDataSources.ComponentResult)
                return BadRequest(new { message = $"{component.Name} data is entered through the Preventive Maintenance (PowerAndAC) screen." });

            var group = await _db.PowerAcEngineerGroups.FirstOrDefaultAsync(g => g.Id == dto.EngineerGroupId);
            if (group is null) return BadRequest(new { message = "Engineer group not found." });

            var manageable = await _access.GetManageableGroupIdsAsync(User);
            if (!manageable.Contains(group.Id)) return Forbid();

            var fieldError = ValidateFields(component, dto);
            if (fieldError != null) return BadRequest(new { message = fieldError });
            var today = DateTime.UtcNow;
            if (PowerAcPeriod.Key(dto.Year, dto.Month) > PowerAcPeriod.Key(today.Year, today.Month))
                return BadRequest(new { message = "Data cannot be entered for a future month." });

            var isAdmin = PowerAcAccessService.IsAdmin(User);
            var user = PowerAcAccessService.CurrentUserName(User);
            var now = DateTime.UtcNow;

            var record = await _db.PowerAcComponentResults.FirstOrDefaultAsync(r =>
                r.EngineerGroupId == dto.EngineerGroupId && r.Year == dto.Year && r.Month == dto.Month && r.ComponentId == dto.ComponentId);

            var previousStatus = record?.Status;
            if (record is null)
            {
                record = new PowerAcComponentResult
                {
                    EngineerGroupId = dto.EngineerGroupId,
                    Year = (short)dto.Year,
                    Month = (byte)dto.Month,
                    ComponentId = dto.ComponentId,
                    CreatedAt = now,
                    CreatedBy = user
                };
                _db.PowerAcComponentResults.Add(record);
            }
            else
            {
                if (!CanEditRecord(record, manageable, isAdmin))
                    return Conflict(new { message = $"The record is {record.Status}. Only an Admin can change it now." });
                record.UpdatedAt = now;
                record.UpdatedBy = user;
            }

            record.ApplicableCount = dto.ApplicableCount;
            record.CompletedCount = dto.CompletedCount;
            record.IncidentCount = dto.IncidentCount;
            record.AchievementPercent = dto.AchievementPercent;
            record.Remarks = dto.Remarks?.Trim();
            record.Status = PowerAcRecordStatus.Draft;
            record.SubmittedAt = null;
            record.SubmittedBy = null;
            record.VerifiedAt = null;
            record.VerifiedBy = null;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "A record already exists for this engineer group, month and component." });
            }

            PowerAcAudit.Add(_db, PowerAcAuditEntities.ComponentResult, record.Id,
                previousStatus is null ? PowerAcAuditActions.Created : PowerAcAuditActions.Updated,
                previousStatus, record.Status, user,
                new { record.EngineerGroupId, record.Year, record.Month, record.ComponentId, record.ApplicableCount, record.CompletedCount, record.IncidentCount, record.AchievementPercent, record.Remarks });
            await _db.SaveChangesAsync();

            await _db.Entry(record).Reference(r => r.EngineerGroup).LoadAsync();
            await _db.Entry(record).Reference(r => r.Component).LoadAsync();
            return Ok(ToDto(record, true));
        }

        [HttpPost("component-results/{id:int}/submit")]
        public async Task<ActionResult<PowerAcComponentResultDto>> Submit(int id)
        {
            var record = await _db.PowerAcComponentResults.Include(r => r.Component).Include(r => r.EngineerGroup).FirstOrDefaultAsync(r => r.Id == id);
            if (record is null) return NotFound(new { message = "Record not found." });
            if (!await _access.CanManageGroupAsync(User, record.EngineerGroupId)) return Forbid();
            if (record.Status != PowerAcRecordStatus.Draft && record.Status != PowerAcRecordStatus.Rejected)
                return Conflict(new { message = $"Only Draft or Rejected records can be submitted. This record is {record.Status}." });

            var error = PowerAcRules.ValidateForSubmission(record.Component!, record);
            if (error != null) return BadRequest(new { message = error });

            var fromStatus = record.Status;
            record.Status = PowerAcRecordStatus.Submitted;
            record.SubmittedAt = DateTime.UtcNow;
            record.SubmittedBy = PowerAcAccessService.CurrentUserName(User);
            record.ReviewComment = null;
            PowerAcAudit.Add(_db, PowerAcAuditEntities.ComponentResult, record.Id, PowerAcAuditActions.Submitted,
                fromStatus, record.Status, record.SubmittedBy,
                new { record.ApplicableCount, record.CompletedCount, record.IncidentCount, record.AchievementPercent });
            await _db.SaveChangesAsync();
            return Ok(ToDto(record, false));
        }

        [HttpPost("component-results/{id:int}/verify")]
        public async Task<ActionResult<PowerAcComponentResultDto>> Verify(int id, [FromBody] VerifyPowerAcComponentResultDto dto)
        {
            if (!await _access.CanVerifyAsync(User)) return Forbid();
            var record = await _db.PowerAcComponentResults.Include(r => r.Component).Include(r => r.EngineerGroup).FirstOrDefaultAsync(r => r.Id == id);
            if (record is null) return NotFound(new { message = "Record not found." });
            if (record.Status != PowerAcRecordStatus.Submitted)
                return Conflict(new { message = $"Only Submitted records can be reviewed. This record is {record.Status}." });

            var user = PowerAcAccessService.CurrentUserName(User);
            if (user != null && (string.Equals(user, record.SubmittedBy, StringComparison.OrdinalIgnoreCase)
                || string.Equals(user, record.UpdatedBy ?? record.CreatedBy, StringComparison.OrdinalIgnoreCase)))
                return Conflict(new { message = "A record must be verified by someone other than the person who entered or submitted it." });
            if (!dto.Approve && string.IsNullOrWhiteSpace(dto.Comment))
                return BadRequest(new { message = "A comment is required when rejecting a record." });

            var fromStatus = record.Status;
            record.Status = dto.Approve ? PowerAcRecordStatus.Verified : PowerAcRecordStatus.Rejected;
            record.ReviewComment = dto.Comment?.Trim();
            record.VerifiedAt = DateTime.UtcNow;
            record.VerifiedBy = user;
            PowerAcAudit.Add(_db, PowerAcAuditEntities.ComponentResult, record.Id,
                dto.Approve ? PowerAcAuditActions.Verified : PowerAcAuditActions.Rejected,
                fromStatus, record.Status, user,
                new { record.ApplicableCount, record.CompletedCount, record.IncidentCount, record.AchievementPercent, record.SubmittedBy },
                record.ReviewComment);
            await _db.SaveChangesAsync();
            return Ok(ToDto(record, false));
        }

        // Every status change and value change of one record, oldest first.
        [HttpGet("component-results/{id:int}/history")]
        public async Task<ActionResult<List<PowerAcAuditEntryDto>>> GetHistory(int id)
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            if (!await _db.PowerAcComponentResults.AnyAsync(r => r.Id == id)) return NotFound(new { message = "Record not found." });
            var rows = await _db.PowerAcAuditLogs.AsNoTracking()
                .Where(a => a.EntityType == PowerAcAuditEntities.ComponentResult && a.EntityId == id)
                .OrderBy(a => a.ChangedAt).ThenBy(a => a.Id)
                .ToListAsync();
            return Ok(rows.Select(PowerAcConfigController.ToDto).ToList());
        }

        // ---------------- Helpers ----------------

        private static string? ValidateFields(PowerAcComponent component, SavePowerAcComponentResultDto dto)
        {
            if (PowerAcRules.IsIncidentComponent(component))
            {
                if (dto.ApplicableCount != null || dto.CompletedCount != null || dto.AchievementPercent != null)
                    return "Critical Safety records hold an incident count only.";
                return null;
            }

            if (dto.IncidentCount != null)
                return $"{component.Name} records do not hold an incident count.";
            if ((dto.ApplicableCount is null) != (dto.CompletedCount is null))
                return "Applicable and completed counts must be entered together.";
            return null;
        }

        private static bool CanEditRecord(PowerAcComponentResult r, HashSet<int> manageable, bool isAdmin)
        {
            if (!manageable.Contains(r.EngineerGroupId)) return false;
            if (isAdmin) return true;
            return r.Status == PowerAcRecordStatus.Draft || r.Status == PowerAcRecordStatus.Rejected;
        }

        private static PowerAcComponentResultDto ToDto(PowerAcComponentResult r, bool canEdit) => new()
        {
            Id = r.Id,
            EngineerGroupId = r.EngineerGroupId,
            EngineerGroupCode = r.EngineerGroup?.Code ?? string.Empty,
            Year = r.Year,
            Month = r.Month,
            ComponentId = r.ComponentId,
            ComponentCode = r.Component?.Code ?? string.Empty,
            ApplicableCount = r.ApplicableCount,
            CompletedCount = r.CompletedCount,
            IncidentCount = r.IncidentCount,
            AchievementPercent = r.AchievementPercent,
            Status = r.Status,
            Remarks = r.Remarks,
            ReviewComment = r.ReviewComment,
            SubmittedBy = r.SubmittedBy,
            SubmittedAt = r.SubmittedAt,
            VerifiedBy = r.VerifiedBy,
            VerifiedAt = r.VerifiedAt,
            CreatedAt = r.CreatedAt,
            CreatedBy = r.CreatedBy,
            UpdatedAt = r.UpdatedAt,
            UpdatedBy = r.UpdatedBy,
            CanEdit = canEdit
        };
    }
}
