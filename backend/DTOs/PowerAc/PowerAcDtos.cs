/*
 * File: PowerAcDtos.cs
 * Request and response contracts for the Power & AC KPI API (api/power-ac/...).
 */
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.PowerAc
{
    // ---------- Configuration ----------

    public class PowerAcComponentDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal TargetValue { get; set; }
        public string TargetUnit { get; set; } = string.Empty;
        public string DataSource { get; set; } = string.Empty;
        public string ScoringMethod { get; set; } = string.Empty;
        public decimal? ScoringParameter { get; set; }
        public bool ScoringApproved { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public decimal? CurrentWeightPercent { get; set; }
        public string[] AllowedScoringMethods { get; set; } = Array.Empty<string>();
        // Scoring version these values come from (null when no version is effective for the month asked).
        public int? VersionId { get; set; }
        public int? VersionEffectiveYear { get; set; }
        public int? VersionEffectiveMonth { get; set; }
        public bool HasEffectiveVersion { get; set; }
    }

    public class PowerAcComponentVersionDto
    {
        public int Id { get; set; }
        public int ComponentId { get; set; }
        public string ComponentCode { get; set; } = string.Empty;
        public int EffectiveYear { get; set; }
        public int EffectiveMonth { get; set; }
        public decimal TargetValue { get; set; }
        public string ScoringMethod { get; set; } = string.Empty;
        public decimal? ScoringParameter { get; set; }
        public bool ScoringApproved { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovalNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class PowerAcAuditEntryDto
    {
        public long Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public string? DetailsJson { get; set; }
        public string? Comment { get; set; }
        public string? ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class UpdatePowerAcComponentDto
    {
        // The scoring version applies from this month onwards.
        [Range(2000, 9999)]
        public int EffectiveYear { get; set; }

        [Range(1, 12)]
        public int EffectiveMonth { get; set; }

        [StringLength(500)]
        public string? ApprovalNote { get; set; }

        [Range(0, 100000)]
        public decimal TargetValue { get; set; }

        [Required, StringLength(40)]
        public string ScoringMethod { get; set; } = string.Empty;

        [Range(0, 100)]
        public decimal? ScoringParameter { get; set; }

        // Approval must be given explicitly; changing the method clears an earlier approval.
        public bool ScoringApproved { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, 1000)]
        public int SortOrder { get; set; }
    }

    public class PowerAcWeightItemDto
    {
        [Range(1, int.MaxValue)]
        public int ComponentId { get; set; }

        [Range(0, 100)]
        public decimal WeightPercent { get; set; }
    }

    public class SavePowerAcWeightsDto
    {
        [Range(2000, 9999)]
        public int EffectiveYear { get; set; }

        [Range(1, 12)]
        public int EffectiveMonth { get; set; }

        [Required, MinLength(1)]
        public List<PowerAcWeightItemDto> Weights { get; set; } = new();
    }

    public class PowerAcWeightDto
    {
        public int Id { get; set; }
        public int ComponentId { get; set; }
        public string ComponentCode { get; set; } = string.Empty;
        public int EffectiveYear { get; set; }
        public int EffectiveMonth { get; set; }
        public decimal WeightPercent { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class PowerAcEffectiveWeightsDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Total { get; set; }
        public bool IsValid { get; set; }
        public List<PowerAcWeightDto> Effective { get; set; } = new();
        public List<PowerAcWeightDto> History { get; set; } = new();
    }

    public class PowerAcSettingDto
    {
        public int Id { get; set; }
        public int EffectiveYear { get; set; }
        public int EffectiveMonth { get; set; }
        public int KpiDefinitionId { get; set; }
        public string? KpiName { get; set; }
        public int? PointsApplicable { get; set; }
        public string PointsMethod { get; set; } = string.Empty;
        public decimal CompositeTarget { get; set; }
        public bool PointsMethodApproved { get; set; }
        public bool RequireVerifiedData { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class SavePowerAcSettingDto
    {
        [Range(2000, 9999)]
        public int EffectiveYear { get; set; }

        [Range(1, 12)]
        public int EffectiveMonth { get; set; }

        [Range(1, int.MaxValue)]
        public int KpiDefinitionId { get; set; }

        [Required, StringLength(30)]
        public string PointsMethod { get; set; } = string.Empty;

        [Range(0.0001, 100)]
        public decimal CompositeTarget { get; set; } = 1m;

        public bool PointsMethodApproved { get; set; }

        public bool RequireVerifiedData { get; set; } = true;
    }

    public class PowerAcEngineerGroupDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string WorkbookLabel { get; set; } = string.Empty;
        public string WorkbookAllocationCell { get; set; } = string.Empty;
        public int? RegionDataId { get; set; }
        public string? NetworkEngineer { get; set; }
        public string? LeaCode { get; set; }
        public string? Region { get; set; }
        public string? Province { get; set; }
        public string? EngineerName { get; set; }
        public string? PmDesignation { get; set; }
        public bool MappingVerified { get; set; }
        public string? MappingNote { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool CanManage { get; set; }
    }

    public class UpdatePowerAcGroupMappingDto
    {
        public int? RegionDataId { get; set; }

        [StringLength(100)]
        public string? PmDesignation { get; set; }

        public bool MappingVerified { get; set; }

        [StringLength(500)]
        public string? MappingNote { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class PowerAcGroupAssignmentDto
    {
        public int Id { get; set; }
        public int EngineerGroupId { get; set; }
        public string EngineerGroupCode { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? ServiceId { get; set; }
        public DateTime AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
    }

    public class CreatePowerAcGroupAssignmentDto
    {
        [Range(1, int.MaxValue)]
        public int EngineerGroupId { get; set; }

        [Range(1, int.MaxValue)]
        public int UserId { get; set; }
    }

    // ---------- Allocations ----------

    public class PowerAcAllocationDto
    {
        public int EngineerGroupId { get; set; }
        public string EngineerGroupCode { get; set; } = string.Empty;
        public string WorkbookLabel { get; set; } = string.Empty;
        public string WorkbookAllocationCell { get; set; } = string.Empty;
        public string? NetworkEngineer { get; set; }
        public string? LeaCode { get; set; }
        public bool MappingVerified { get; set; }
        public int? AllocationId { get; set; }
        public decimal? AllocationValue { get; set; }
        public int? EffectiveYear { get; set; }
        public int? EffectiveMonth { get; set; }
        public string? Remarks { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public bool CanEdit { get; set; }
    }

    public class PowerAcAllocationSummaryDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Total { get; set; }
        public int MissingCount { get; set; }
        public bool IsComplete { get; set; }
        public List<PowerAcAllocationDto> Items { get; set; } = new();
    }

    public class PowerAcAllocationItemDto
    {
        [Range(1, int.MaxValue)]
        public int EngineerGroupId { get; set; }

        [Range(0, 1000000)]
        public decimal AllocationValue { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    public class SavePowerAcAllocationsDto
    {
        [Range(2000, 9999)]
        public int EffectiveYear { get; set; }

        [Range(1, 12)]
        public int EffectiveMonth { get; set; }

        [Required, MinLength(1)]
        public List<PowerAcAllocationItemDto> Items { get; set; } = new();
    }

    // ---------- Component data ----------

    public class PowerAcComponentResultDto
    {
        public int Id { get; set; }
        public int EngineerGroupId { get; set; }
        public string EngineerGroupCode { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public int ComponentId { get; set; }
        public string ComponentCode { get; set; } = string.Empty;
        public decimal? ApplicableCount { get; set; }
        public decimal? CompletedCount { get; set; }
        public int? IncidentCount { get; set; }
        public decimal? AchievementPercent { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public string? ReviewComment { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public bool CanEdit { get; set; }
    }

    public class SavePowerAcComponentResultDto
    {
        [Range(1, int.MaxValue)]
        public int EngineerGroupId { get; set; }

        [Range(2000, 9999)]
        public int Year { get; set; }

        [Range(1, 12)]
        public int Month { get; set; }

        [Range(1, int.MaxValue)]
        public int ComponentId { get; set; }

        [Range(0, 1000000)]
        public decimal? ApplicableCount { get; set; }

        [Range(0, 1000000)]
        public decimal? CompletedCount { get; set; }

        [Range(0, 100000)]
        public int? IncidentCount { get; set; }

        [Range(0, 100)]
        public decimal? AchievementPercent { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    public class VerifyPowerAcComponentResultDto
    {
        public bool Approve { get; set; }

        [StringLength(500)]
        public string? Comment { get; set; }
    }

    // ---------- Results ----------

    public class PowerAcComponentScoreDto
    {
        public int ComponentId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? WeightPercent { get; set; }
        public int? ComponentVersionId { get; set; }
        public string ScoringMethod { get; set; } = string.Empty;
        public decimal? ApplicableCount { get; set; }
        public decimal? CompletedCount { get; set; }
        public int? IncidentCount { get; set; }
        public decimal? AchievementRatio { get; set; }
        public decimal? Score { get; set; }
        public decimal? WeightedScore { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
    }

    public class PowerAcGroupResultDto
    {
        public int EngineerGroupId { get; set; }
        public string EngineerGroupCode { get; set; } = string.Empty;
        public string WorkbookLabel { get; set; } = string.Empty;
        public string? NetworkEngineer { get; set; }
        public string? LeaCode { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal? CompositeAchievement { get; set; }
        public decimal? PointsApplicable { get; set; }
        public decimal? AllocationValue { get; set; }
        public decimal? TotalAllocation { get; set; }
        public decimal? MaximumPoints { get; set; }
        public decimal? PointsAchieved { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<PowerAcComponentScoreDto> Components { get; set; } = new();
    }

    public class PowerAcKpiResultsDto
    {
        public int Year { get; set; }
        public string PeriodType { get; set; } = string.Empty;
        public int PeriodNumber { get; set; }
        public bool IsPreview { get; set; }
        public DateTime? CalculatedAt { get; set; }
        public string? CalculatedBy { get; set; }
        public string PointsMethod { get; set; } = string.Empty;
        public decimal CompositeTarget { get; set; }
        public bool PointsMethodApproved { get; set; }
        public bool RequireVerifiedData { get; set; }
        public decimal? PointsApplicable { get; set; }
        public decimal? TotalAllocation { get; set; }
        public bool AllocationsComplete { get; set; }
        public bool WeightsValid { get; set; }
        public decimal WeightTotal { get; set; }
        public List<string> GlobalIssues { get; set; } = new();
        public List<PowerAcComponentDto> Components { get; set; } = new();
        public List<PowerAcGroupResultDto> Groups { get; set; } = new();
        public int CompleteCount { get; set; }
        public int IncompleteCount { get; set; }
        public int PendingCount { get; set; }
        public decimal? TotalPointsAchieved { get; set; }
        public decimal? TotalMaximumPoints { get; set; }
    }
}
