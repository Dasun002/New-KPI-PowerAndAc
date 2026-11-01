/*
 * File: PowerAcConfigController.cs
 * Power & AC KPI configuration: components, effective-dated weights, calculation settings,
 * engineer group mappings and engineer group user assignments.
 * Reads need page 11 view access; every change needs the AdminOnly policy (Admin or SuperAdmin).
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
    [Route("api/power-ac/config")]
    [Authorize]
    public class PowerAcConfigController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly PowerAcAccessService _access;

        public PowerAcConfigController(AppDbContext db, PowerAcAccessService access)
        {
            _db = db;
            _access = access;
        }

        // ---------------- Components ----------------

        // Components with the scoring version effective for the given month (default: current month).
        [HttpGet("components")]
        public async Task<ActionResult<List<PowerAcComponentDto>>> GetComponents([FromQuery] int? year, [FromQuery] int? month)
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            var now = DateTime.UtcNow;
            var key = PowerAcPeriod.Key(year ?? now.Year, month ?? now.Month);

            var components = await _db.PowerAcComponents.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync();
            var weights = await _db.PowerAcComponentWeights.AsNoTracking().ToListAsync();
            var versions = await _db.PowerAcComponentVersions.AsNoTracking().ToListAsync();
            return Ok(components.Select(c => ToDto(c, EffectiveWeight(weights, c.Id, key)?.WeightPercent, EffectiveVersion(versions, c.Id, key))).ToList());
        }

        // Every scoring version of a component, newest first, with who created and approved it.
        [HttpGet("components/{id:int}/versions")]
        public async Task<ActionResult<List<PowerAcComponentVersionDto>>> GetComponentVersions(int id)
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            var component = await _db.PowerAcComponents.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (component is null) return NotFound(new { message = "Component not found." });
            var versions = await _db.PowerAcComponentVersions.AsNoTracking()
                .Where(v => v.ComponentId == id)
                .OrderByDescending(v => v.EffectiveYear).ThenByDescending(v => v.EffectiveMonth)
                .ToListAsync();
            return Ok(versions.Select(v => ToDto(v, component)).ToList());
        }

        // Creates or updates the scoring version effective from the given month.
        // Approval is an explicit Admin decision; it is recorded with who approved it and when.
        [HttpPut("components/{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<PowerAcComponentDto>> UpdateComponent(int id, [FromBody] UpdatePowerAcComponentDto dto)
        {
            var component = await _db.PowerAcComponents.FirstOrDefaultAsync(c => c.Id == id);
            if (component is null) return NotFound(new { message = "Component not found." });

            var method = PowerAcScoringMethods.All.FirstOrDefault(m => string.Equals(m, dto.ScoringMethod?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (method is null || !PowerAcRules.AllowedScoringMethods(component).Contains(method))
                return BadRequest(new { message = $"Scoring method '{dto.ScoringMethod}' is not allowed for {component.Code}." });
            if (method == PowerAcScoringMethods.DeductionPerIncident && dto.ScoringParameter is null)
                return BadRequest(new { message = "DeductionPerIncident needs a deduction percentage per incident." });
            if (method != PowerAcScoringMethods.DeductionPerIncident && dto.ScoringParameter is not null)
                return BadRequest(new { message = "A scoring parameter is only used by DeductionPerIncident." });
            if (method == PowerAcScoringMethods.Pending && dto.ScoringApproved)
                return BadRequest(new { message = "A Pending scoring method cannot be approved." });
            if (method == PowerAcScoringMethods.TargetScaled && dto.TargetValue <= 0m)
                return BadRequest(new { message = "TargetScaled scoring needs a target above zero." });
            if (await PowerAcRules.HasCalculatedResultsFromAsync(_db, dto.EffectiveYear, dto.EffectiveMonth))
                return Conflict(new { message = "Results have already been calculated for this effective month or later. Choose a later effective month so earlier results stay reproducible." });

            var user = PowerAcAccessService.CurrentUserName(User);
            var now = DateTime.UtcNow;
            var version = await _db.PowerAcComponentVersions.FirstOrDefaultAsync(v =>
                v.ComponentId == id && v.EffectiveYear == dto.EffectiveYear && v.EffectiveMonth == dto.EffectiveMonth);
            var isNew = version is null;
            var wasApproved = version?.ScoringApproved ?? false;
            var scoringChanged = isNew
                || version!.ScoringMethod != method
                || version.ScoringParameter != dto.ScoringParameter
                || version.TargetValue != dto.TargetValue;

            if (isNew)
            {
                version = new PowerAcComponentVersion
                {
                    ComponentId = id,
                    EffectiveYear = (short)dto.EffectiveYear,
                    EffectiveMonth = (byte)dto.EffectiveMonth,
                    CreatedAt = now,
                    CreatedBy = user
                };
                _db.PowerAcComponentVersions.Add(version);
            }
            else
            {
                version!.UpdatedAt = now;
                version.UpdatedBy = user;
            }

            version.TargetValue = dto.TargetValue;
            version.ScoringMethod = method;
            version.ScoringParameter = dto.ScoringParameter;

            // Approval belongs to one exact method/target/parameter combination: any change needs a new approval.
            var approvalAction = (string?)null;
            if (dto.ScoringApproved && (scoringChanged || !wasApproved))
            {
                version.ScoringApproved = true;
                version.ApprovedBy = user;
                version.ApprovedAt = now;
                version.ApprovalNote = dto.ApprovalNote?.Trim();
                approvalAction = PowerAcAuditActions.Approved;
            }
            else if (!dto.ScoringApproved)
            {
                if (wasApproved) approvalAction = PowerAcAuditActions.ApprovalWithdrawn;
                version.ScoringApproved = false;
                version.ApprovedBy = null;
                version.ApprovedAt = null;
                version.ApprovalNote = null;
            }

            component.IsActive = dto.IsActive;
            component.SortOrder = dto.SortOrder;
            component.UpdatedAt = now;
            component.UpdatedBy = user;
            await _db.SaveChangesAsync();

            var details = new { component = component.Code, version.EffectiveYear, version.EffectiveMonth, version.TargetValue, version.ScoringMethod, version.ScoringParameter, dto.IsActive };
            PowerAcAudit.Add(_db, PowerAcAuditEntities.ComponentVersion, version.Id,
                isNew ? PowerAcAuditActions.Created : PowerAcAuditActions.Updated, null, null, user, details);
            if (approvalAction != null)
                PowerAcAudit.Add(_db, PowerAcAuditEntities.ComponentVersion, version.Id, approvalAction,
                    wasApproved ? "Approved" : "Unapproved", version.ScoringApproved ? "Approved" : "Unapproved", user, details, dto.ApprovalNote);

            await SyncComponentMirrorAsync(component);
            await _db.SaveChangesAsync();

            var weights = await _db.PowerAcComponentWeights.AsNoTracking().ToListAsync();
            var key = PowerAcPeriod.Key(dto.EffectiveYear, dto.EffectiveMonth);
            return Ok(ToDto(component, EffectiveWeight(weights, component.Id, key)?.WeightPercent, version));
        }

        // Keeps the component row's summary columns equal to the version effective this month (display only;
        // calculations always resolve the version effective for their own period).
        private async Task SyncComponentMirrorAsync(PowerAcComponent component)
        {
            var now = DateTime.UtcNow;
            var current = EffectiveVersion(await _db.PowerAcComponentVersions.Where(v => v.ComponentId == component.Id).ToListAsync(),
                component.Id, PowerAcPeriod.Key(now.Year, now.Month));
            if (current is null) return;
            component.TargetValue = current.TargetValue;
            component.ScoringMethod = current.ScoringMethod;
            component.ScoringParameter = current.ScoringParameter;
            component.ScoringApproved = current.ScoringApproved;
            component.ApprovedBy = current.ApprovedBy;
            component.ApprovedAt = current.ApprovedAt;
        }

        // Audit trail for configuration entities (Admin only).
        [HttpGet("audit")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<List<PowerAcAuditEntryDto>>> GetAudit([FromQuery] string? entityType, [FromQuery] int? entityId, [FromQuery] int take = 200)
        {
            var q = _db.PowerAcAuditLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
            if (entityId.HasValue) q = q.Where(a => a.EntityId == entityId.Value);
            var rows = await q.OrderByDescending(a => a.ChangedAt).ThenByDescending(a => a.Id).Take(Math.Clamp(take, 1, 1000)).ToListAsync();
            return Ok(rows.Select(ToDto).ToList());
        }

        // ---------------- Weights ----------------

        [HttpGet("weights")]
        public async Task<ActionResult<PowerAcEffectiveWeightsDto>> GetWeights([FromQuery] int? year, [FromQuery] int? month)
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            var now = DateTime.UtcNow;
            var y = year ?? now.Year;
            var m = month ?? now.Month;
            if (m < 1 || m > 12) return BadRequest(new { message = "month must be between 1 and 12." });

            var components = await _db.PowerAcComponents.AsNoTracking().ToListAsync();
            var weights = await _db.PowerAcComponentWeights.AsNoTracking().ToListAsync();
            var key = PowerAcPeriod.Key(y, m);

            var effective = components.Where(c => c.IsActive)
                .Select(c => EffectiveWeight(weights, c.Id, key))
                .Where(w => w != null)
                .Select(w => ToDto(w!, components))
                .ToList();
            var activeCount = components.Count(c => c.IsActive);
            var total = effective.Sum(w => w.WeightPercent);

            return Ok(new PowerAcEffectiveWeightsDto
            {
                Year = y,
                Month = m,
                Total = total,
                IsValid = effective.Count == activeCount && activeCount > 0 && Math.Abs(total - 100m) <= 0.0001m,
                Effective = effective,
                History = weights.OrderByDescending(w => w.EffectiveYear).ThenByDescending(w => w.EffectiveMonth).ThenBy(w => w.ComponentId)
                    .Select(w => ToDto(w, components)).ToList()
            });
        }

        [HttpPost("weights")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> SaveWeights([FromBody] SavePowerAcWeightsDto dto)
        {
            var active = await _db.PowerAcComponents.Where(c => c.IsActive).ToListAsync();
            if (active.Count == 0) return BadRequest(new { message = "No active components are configured." });

            if (dto.Weights.GroupBy(w => w.ComponentId).Any(g => g.Count() > 1))
                return BadRequest(new { message = "Each component may appear only once." });

            var activeIds = active.Select(c => c.Id).ToHashSet();
            var unknown = dto.Weights.Where(w => !activeIds.Contains(w.ComponentId)).Select(w => w.ComponentId).ToList();
            if (unknown.Count > 0)
                return BadRequest(new { message = $"Weights were given for unknown or inactive components: {string.Join(", ", unknown)}." });

            var missing = active.Where(c => dto.Weights.All(w => w.ComponentId != c.Id)).Select(c => c.Code).ToList();
            if (missing.Count > 0)
                return BadRequest(new { message = $"A weight is required for every active component. Missing: {string.Join(", ", missing)}." });

            var total = dto.Weights.Sum(w => w.WeightPercent);
            if (Math.Abs(total - 100m) > 0.0001m)
                return BadRequest(new { message = $"Weights total {total:0.####}%. They must total 100%." });

            if (await PowerAcRules.HasCalculatedResultsFromAsync(_db, dto.EffectiveYear, dto.EffectiveMonth))
                return Conflict(new { message = "Results have already been calculated for this effective month or later. Choose a later effective month so earlier results stay reproducible." });

            var user = PowerAcAccessService.CurrentUserName(User);
            var now = DateTime.UtcNow;
            var existing = await _db.PowerAcComponentWeights
                .Where(w => w.EffectiveYear == dto.EffectiveYear && w.EffectiveMonth == dto.EffectiveMonth)
                .ToListAsync();

            foreach (var item in dto.Weights)
            {
                var row = existing.FirstOrDefault(w => w.ComponentId == item.ComponentId);
                if (row is null)
                {
                    _db.PowerAcComponentWeights.Add(new PowerAcComponentWeight
                    {
                        ComponentId = item.ComponentId,
                        EffectiveYear = (short)dto.EffectiveYear,
                        EffectiveMonth = (byte)dto.EffectiveMonth,
                        WeightPercent = item.WeightPercent,
                        CreatedAt = now,
                        CreatedBy = user
                    });
                }
                else
                {
                    row.WeightPercent = item.WeightPercent;
                    row.CreatedAt = now;
                    row.CreatedBy = user;
                }
            }

            await _db.SaveChangesAsync();
            PowerAcAudit.Add(_db, PowerAcAuditEntities.ComponentWeight, PowerAcPeriod.Key(dto.EffectiveYear, dto.EffectiveMonth),
                existing.Count == 0 ? PowerAcAuditActions.Created : PowerAcAuditActions.Updated, null, null, user,
                new { dto.EffectiveYear, dto.EffectiveMonth, weights = dto.Weights.Select(w => new { w.ComponentId, w.WeightPercent }), total });
            await _db.SaveChangesAsync();
            return Ok(new { message = "Weights saved.", effectiveYear = dto.EffectiveYear, effectiveMonth = dto.EffectiveMonth, total });
        }

        // ---------------- Calculation settings ----------------

        [HttpGet("settings")]
        public async Task<ActionResult<List<PowerAcSettingDto>>> GetSettings()
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            var settings = await _db.PowerAcCalculationSettings.AsNoTracking()
                .OrderByDescending(s => s.EffectiveYear).ThenByDescending(s => s.EffectiveMonth).ToListAsync();
            var kpiIds = settings.Select(s => s.KpiDefinitionId).Distinct().ToList();
            var kpis = await _db.KpiDefinitions.AsNoTracking().Where(k => kpiIds.Contains(k.Id)).ToListAsync();

            return Ok(settings.Select(s =>
            {
                var kpi = kpis.FirstOrDefault(k => k.Id == s.KpiDefinitionId);
                return new PowerAcSettingDto
                {
                    Id = s.Id,
                    EffectiveYear = s.EffectiveYear,
                    EffectiveMonth = s.EffectiveMonth,
                    KpiDefinitionId = s.KpiDefinitionId,
                    KpiName = kpi?.KeyPerformanceIndicators,
                    PointsApplicable = kpi?.PointsApplicable,
                    PointsMethod = s.PointsMethod,
                    CompositeTarget = s.CompositeTarget,
                    PointsMethodApproved = s.PointsMethodApproved,
                    RequireVerifiedData = s.RequireVerifiedData,
                    CreatedAt = s.CreatedAt,
                    CreatedBy = s.CreatedBy
                };
            }).ToList());
        }

        [HttpPost("settings")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> SaveSetting([FromBody] SavePowerAcSettingDto dto)
        {
            var method = PowerAcPointsMethods.All.FirstOrDefault(m => string.Equals(m, dto.PointsMethod?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (method is null)
                return BadRequest(new { message = $"Points method must be one of: {string.Join(", ", PowerAcPointsMethods.All)}." });
            if (!await _db.KpiDefinitions.AnyAsync(k => k.Id == dto.KpiDefinitionId))
                return BadRequest(new { message = $"KPI definition {dto.KpiDefinitionId} does not exist." });
            if (await PowerAcRules.HasCalculatedResultsFromAsync(_db, dto.EffectiveYear, dto.EffectiveMonth))
                return Conflict(new { message = "Results have already been calculated for this effective month or later. Choose a later effective month." });

            var user = PowerAcAccessService.CurrentUserName(User);
            var row = await _db.PowerAcCalculationSettings
                .FirstOrDefaultAsync(s => s.EffectiveYear == dto.EffectiveYear && s.EffectiveMonth == dto.EffectiveMonth);
            if (row is null)
            {
                row = new PowerAcCalculationSetting { EffectiveYear = (short)dto.EffectiveYear, EffectiveMonth = (byte)dto.EffectiveMonth };
                _db.PowerAcCalculationSettings.Add(row);
            }
            row.KpiDefinitionId = dto.KpiDefinitionId;
            row.PointsMethod = method;
            row.CompositeTarget = dto.CompositeTarget;
            row.PointsMethodApproved = dto.PointsMethodApproved;
            row.RequireVerifiedData = dto.RequireVerifiedData;
            row.CreatedAt = DateTime.UtcNow;
            row.CreatedBy = user;

            await _db.SaveChangesAsync();
            PowerAcAudit.Add(_db, PowerAcAuditEntities.CalculationSetting, row.Id, PowerAcAuditActions.Updated, null,
                row.PointsMethodApproved ? "Approved" : "Unapproved", user,
                new { row.EffectiveYear, row.EffectiveMonth, row.KpiDefinitionId, row.PointsMethod, row.CompositeTarget, row.PointsMethodApproved, row.RequireVerifiedData });
            await _db.SaveChangesAsync();
            return Ok(new { message = "Settings saved.", id = row.Id });
        }

        // ---------------- Engineer groups ----------------

        [HttpGet("engineer-groups")]
        public async Task<ActionResult<List<PowerAcEngineerGroupDto>>> GetEngineerGroups()
        {
            if (!await _access.CanViewAsync(User)) return Forbid();
            var manageable = await _access.GetManageableGroupIdsAsync(User);
            var groups = await _db.PowerAcEngineerGroups.AsNoTracking()
                .Include(g => g.RegionData)
                .OrderBy(g => g.SortOrder).ThenBy(g => g.Id)
                .ToListAsync();
            return Ok(groups.Select(g => ToDto(g, manageable.Contains(g.Id))).ToList());
        }

        [HttpPut("engineer-groups/{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<PowerAcEngineerGroupDto>> UpdateEngineerGroup(int id, [FromBody] UpdatePowerAcGroupMappingDto dto)
        {
            var group = await _db.PowerAcEngineerGroups.FirstOrDefaultAsync(g => g.Id == id);
            if (group is null) return NotFound(new { message = "Engineer group not found." });

            if (dto.RegionDataId is int regionId)
            {
                if (!await _db.RegionData.AnyAsync(r => r.Id == regionId))
                    return BadRequest(new { message = $"regiondata record {regionId} does not exist." });
                if (await _db.PowerAcEngineerGroups.AnyAsync(g => g.Id != id && g.RegionDataId == regionId))
                    return Conflict(new { message = "That regiondata record is already mapped to another engineer group." });
            }

            var designation = string.IsNullOrWhiteSpace(dto.PmDesignation) ? null : dto.PmDesignation.Trim();
            if (designation != null && await _db.PowerAcEngineerGroups.AnyAsync(g => g.Id != id && g.PmDesignation == designation))
                return Conflict(new { message = "That PowerAndAC designation is already mapped to another engineer group." });
            if (dto.MappingVerified && (dto.RegionDataId is null || designation is null))
                return BadRequest(new { message = "A mapping can only be verified when both regiondata and the PowerAndAC designation are set." });

            group.RegionDataId = dto.RegionDataId;
            group.PmDesignation = designation;
            group.MappingVerified = dto.MappingVerified;
            group.MappingNote = dto.MappingNote?.Trim();
            group.IsActive = dto.IsActive;
            group.UpdatedAt = DateTime.UtcNow;
            group.UpdatedBy = PowerAcAccessService.CurrentUserName(User);
            PowerAcAudit.Add(_db, PowerAcAuditEntities.EngineerGroup, group.Id, PowerAcAuditActions.Updated,
                null, group.MappingVerified ? "Verified" : "Unverified", group.UpdatedBy,
                new { group.RegionDataId, group.PmDesignation, group.MappingVerified, group.IsActive, group.MappingNote });
            await _db.SaveChangesAsync();

            await _db.Entry(group).Reference(g => g.RegionData).LoadAsync();
            return Ok(ToDto(group, true));
        }

        // ---------------- Engineer group user assignments ----------------

        [HttpGet("assignments")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<List<PowerAcGroupAssignmentDto>>> GetAssignments()
        {
            var rows = await _db.PowerAcGroupUserAssignments.AsNoTracking()
                .Include(a => a.EngineerGroup)
                .Include(a => a.User)
                .OrderBy(a => a.EngineerGroupId).ThenBy(a => a.UserId)
                .ToListAsync();
            return Ok(rows.Select(ToDto).ToList());
        }

        [HttpPost("assignments")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<PowerAcGroupAssignmentDto>> CreateAssignment([FromBody] CreatePowerAcGroupAssignmentDto dto)
        {
            if (!await _db.PowerAcEngineerGroups.AnyAsync(g => g.Id == dto.EngineerGroupId))
                return BadRequest(new { message = "Engineer group not found." });
            if (!await _db.Users.AnyAsync(u => u.UserId == dto.UserId && u.IsActive))
                return BadRequest(new { message = "Active user not found." });
            if (await _db.PowerAcGroupUserAssignments.AnyAsync(a => a.EngineerGroupId == dto.EngineerGroupId && a.UserId == dto.UserId))
                return Conflict(new { message = "The user is already assigned to this engineer group." });

            var row = new PowerAcGroupUserAssignment
            {
                EngineerGroupId = dto.EngineerGroupId,
                UserId = dto.UserId,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = PowerAcAccessService.CurrentUserName(User)
            };
            _db.PowerAcGroupUserAssignments.Add(row);
            await _db.SaveChangesAsync();
            PowerAcAudit.Add(_db, PowerAcAuditEntities.GroupAssignment, row.Id, PowerAcAuditActions.Created, null, null,
                row.AssignedBy, new { row.EngineerGroupId, row.UserId });
            await _db.SaveChangesAsync();

            await _db.Entry(row).Reference(a => a.EngineerGroup).LoadAsync();
            await _db.Entry(row).Reference(a => a.User).LoadAsync();
            return Ok(ToDto(row));
        }

        [HttpDelete("assignments/{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteAssignment(int id)
        {
            var row = await _db.PowerAcGroupUserAssignments.FirstOrDefaultAsync(a => a.Id == id);
            if (row is null) return NotFound(new { message = "Assignment not found." });
            _db.PowerAcGroupUserAssignments.Remove(row);
            PowerAcAudit.Add(_db, PowerAcAuditEntities.GroupAssignment, row.Id, PowerAcAuditActions.Deleted, null, null,
                PowerAcAccessService.CurrentUserName(User), new { row.EngineerGroupId, row.UserId });
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ---------------- Mapping helpers ----------------

        internal static PowerAcComponentWeight? EffectiveWeight(IEnumerable<PowerAcComponentWeight> weights, int componentId, int key) =>
            weights.Where(w => w.ComponentId == componentId && PowerAcPeriod.Key(w.EffectiveYear, w.EffectiveMonth) <= key)
                .OrderByDescending(w => PowerAcPeriod.Key(w.EffectiveYear, w.EffectiveMonth))
                .FirstOrDefault();

        internal static PowerAcComponentVersion? EffectiveVersion(IEnumerable<PowerAcComponentVersion> versions, int componentId, int key) =>
            versions.Where(v => v.ComponentId == componentId && PowerAcPeriod.Key(v.EffectiveYear, v.EffectiveMonth) <= key)
                .OrderByDescending(v => PowerAcPeriod.Key(v.EffectiveYear, v.EffectiveMonth))
                .FirstOrDefault();

        // Component with the values of a specific scoring version (or "no version" when none is effective).
        internal static PowerAcComponentDto ToDto(PowerAcComponent c, decimal? weight, PowerAcComponentVersion? version)
        {
            var dto = ToDto(c, weight);
            if (version is null)
            {
                dto.ScoringMethod = PowerAcScoringMethods.Pending;
                dto.ScoringParameter = null;
                dto.ScoringApproved = false;
                dto.ApprovedBy = null;
                dto.ApprovedAt = null;
                dto.HasEffectiveVersion = false;
                return dto;
            }
            dto.TargetValue = version.TargetValue;
            dto.ScoringMethod = version.ScoringMethod;
            dto.ScoringParameter = version.ScoringParameter;
            dto.ScoringApproved = version.ScoringApproved;
            dto.ApprovedBy = version.ApprovedBy;
            dto.ApprovedAt = version.ApprovedAt;
            dto.VersionId = version.Id;
            dto.VersionEffectiveYear = version.EffectiveYear;
            dto.VersionEffectiveMonth = version.EffectiveMonth;
            dto.HasEffectiveVersion = true;
            return dto;
        }

        internal static PowerAcComponentVersionDto ToDto(PowerAcComponentVersion v, PowerAcComponent c) => new()
        {
            Id = v.Id,
            ComponentId = v.ComponentId,
            ComponentCode = c.Code,
            EffectiveYear = v.EffectiveYear,
            EffectiveMonth = v.EffectiveMonth,
            TargetValue = v.TargetValue,
            ScoringMethod = v.ScoringMethod,
            ScoringParameter = v.ScoringParameter,
            ScoringApproved = v.ScoringApproved,
            ApprovedBy = v.ApprovedBy,
            ApprovedAt = v.ApprovedAt,
            ApprovalNote = v.ApprovalNote,
            CreatedAt = v.CreatedAt,
            CreatedBy = v.CreatedBy,
            UpdatedAt = v.UpdatedAt,
            UpdatedBy = v.UpdatedBy
        };

        internal static PowerAcAuditEntryDto ToDto(PowerAcAuditLog a) => new()
        {
            Id = a.Id,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Action = a.Action,
            FromStatus = a.FromStatus,
            ToStatus = a.ToStatus,
            DetailsJson = a.DetailsJson,
            Comment = a.Comment,
            ChangedBy = a.ChangedBy,
            ChangedAt = a.ChangedAt
        };

        internal static PowerAcComponentDto ToDto(PowerAcComponent c, decimal? weight) => new()
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            TargetValue = c.TargetValue,
            TargetUnit = c.TargetUnit,
            DataSource = c.DataSource,
            ScoringMethod = c.ScoringMethod,
            ScoringParameter = c.ScoringParameter,
            ScoringApproved = c.ScoringApproved,
            ApprovedBy = c.ApprovedBy,
            ApprovedAt = c.ApprovedAt,
            SortOrder = c.SortOrder,
            IsActive = c.IsActive,
            CurrentWeightPercent = weight,
            AllowedScoringMethods = PowerAcRules.AllowedScoringMethods(c)
        };

        private static PowerAcWeightDto ToDto(PowerAcComponentWeight w, List<PowerAcComponent> components) => new()
        {
            Id = w.Id,
            ComponentId = w.ComponentId,
            ComponentCode = components.FirstOrDefault(c => c.Id == w.ComponentId)?.Code ?? string.Empty,
            EffectiveYear = w.EffectiveYear,
            EffectiveMonth = w.EffectiveMonth,
            WeightPercent = w.WeightPercent,
            CreatedAt = w.CreatedAt,
            CreatedBy = w.CreatedBy
        };

        internal static PowerAcEngineerGroupDto ToDto(PowerAcEngineerGroup g, bool canManage) => new()
        {
            Id = g.Id,
            Code = g.Code,
            WorkbookLabel = g.WorkbookLabel,
            WorkbookAllocationCell = g.WorkbookAllocationCell,
            RegionDataId = g.RegionDataId,
            NetworkEngineer = g.RegionData?.NetworkEngineer,
            LeaCode = g.RegionData?.LeaCode,
            Region = g.RegionData?.Region,
            Province = g.RegionData?.Province,
            EngineerName = g.RegionData?.EngName,
            PmDesignation = g.PmDesignation,
            MappingVerified = g.MappingVerified,
            MappingNote = g.MappingNote,
            SortOrder = g.SortOrder,
            IsActive = g.IsActive,
            CanManage = canManage
        };

        private static PowerAcGroupAssignmentDto ToDto(PowerAcGroupUserAssignment a) => new()
        {
            Id = a.Id,
            EngineerGroupId = a.EngineerGroupId,
            EngineerGroupCode = a.EngineerGroup?.Code ?? string.Empty,
            UserId = a.UserId,
            UserName = a.User?.Name,
            ServiceId = a.User?.ServiceId,
            AssignedAt = a.AssignedAt,
            AssignedBy = a.AssignedBy
        };
    }
}
