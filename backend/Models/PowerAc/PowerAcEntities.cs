/*
 * File: PowerAcEntities.cs
 * Entity models for the four-component Power & AC KPI.
 * Tables are created by database/power-ac/001_power_ac_kpi_deploy.sql (not by EF migrations).
 * Preventive Maintenance source data stays in the existing dbo.PowerAndAC table.
 */

namespace backend.Models.PowerAc
{
    // One of the four Power & AC KPI components (PM, SAFETY, CORRECTIVE, AVAILABILITY).
    public class PowerAcComponent
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        // Target expressed in TargetUnit (percent for ratio components, incidents for safety).
        public decimal TargetValue { get; set; }
        public string TargetUnit { get; set; } = "%";
        // PowerAndAC (existing PM table) or ComponentResult (dbo.PowerAcComponentResult).
        public string DataSource { get; set; } = PowerAcDataSources.ComponentResult;
        public string ScoringMethod { get; set; } = PowerAcScoringMethods.Pending;
        public decimal? ScoringParameter { get; set; }
        // A component is only scored after its scoring method has been approved.
        public bool ScoringApproved { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    // Effective-dated component weight. The latest row on or before a period applies to that period.
    public class PowerAcComponentWeight
    {
        public int Id { get; set; }
        public int ComponentId { get; set; }
        public PowerAcComponent? Component { get; set; }
        public short EffectiveYear { get; set; }
        public byte EffectiveMonth { get; set; }
        public decimal WeightPercent { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    // One of the 20 engineer groups in 'Power & AC'!C19:C38, linked to regiondata by id.
    public class PowerAcEngineerGroup
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string WorkbookLabel { get; set; } = string.Empty;
        public string WorkbookAllocationCell { get; set; } = string.Empty;
        public int? RegionDataId { get; set; }
        public RegionData? RegionData { get; set; }
        // Exact dbo.PowerAndAC.Designation value used for Preventive Maintenance rows.
        public string? PmDesignation { get; set; }
        // Results are only calculated for verified mappings.
        public bool MappingVerified { get; set; }
        public string? MappingNote { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    // Grants a user permission to enter data for one engineer group.
    public class PowerAcGroupUserAssignment
    {
        public int Id { get; set; }
        public int EngineerGroupId { get; set; }
        public PowerAcEngineerGroup? EngineerGroup { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public DateTime AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
    }

    // Effective-dated area allocation ('Power & AC'!D19:D38).
    public class PowerAcAreaAllocation
    {
        public int Id { get; set; }
        public int EngineerGroupId { get; set; }
        public PowerAcEngineerGroup? EngineerGroup { get; set; }
        public short EffectiveYear { get; set; }
        public byte EffectiveMonth { get; set; }
        public decimal AllocationValue { get; set; }
        public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    // Monthly source data for Critical Safety, Corrective Action and Power System Availability.
    public class PowerAcComponentResult
    {
        public int Id { get; set; }
        public int EngineerGroupId { get; set; }
        public PowerAcEngineerGroup? EngineerGroup { get; set; }
        public short Year { get; set; }
        public byte Month { get; set; }
        public int ComponentId { get; set; }
        public PowerAcComponent? Component { get; set; }
        public decimal? ApplicableCount { get; set; }
        public decimal? CompletedCount { get; set; }
        public int? IncidentCount { get; set; }
        public decimal? AchievementPercent { get; set; }
        public string Status { get; set; } = PowerAcRecordStatus.Draft;
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
    }

    // Effective-dated calculation settings (points formula, verification policy, KPI definition link).
    public class PowerAcCalculationSetting
    {
        public int Id { get; set; }
        public short EffectiveYear { get; set; }
        public byte EffectiveMonth { get; set; }
        public int KpiDefinitionId { get; set; }
        public string PointsMethod { get; set; } = PowerAcPointsMethods.TargetScaled;
        public decimal CompositeTarget { get; set; } = 1m;
        public bool PointsMethodApproved { get; set; }
        public bool RequireVerifiedData { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    // Persisted calculation snapshot for one engineer group and period.
    public class PowerAcKpiResult
    {
        public int Id { get; set; }
        public int EngineerGroupId { get; set; }
        public PowerAcEngineerGroup? EngineerGroup { get; set; }
        public short Year { get; set; }
        public string PeriodType { get; set; } = PowerAcPeriodTypes.Month;
        public byte PeriodNumber { get; set; }
        public string Status { get; set; } = PowerAcResultStatus.Incomplete;
        public decimal? CompositeAchievement { get; set; }
        public decimal? PointsApplicable { get; set; }
        public decimal? AllocationValue { get; set; }
        public decimal? TotalAllocation { get; set; }
        public decimal? MaximumPoints { get; set; }
        public decimal? PointsAchieved { get; set; }
        public string PointsMethod { get; set; } = string.Empty;
        public decimal CompositeTarget { get; set; }
        // Settings row (points formula, approval, verification policy) used for this snapshot.
        public int? CalculationSettingId { get; set; }
        public string? IssuesJson { get; set; }
        public DateTime CalculatedAt { get; set; }
        public string? CalculatedBy { get; set; }
        public List<PowerAcKpiComponentScore> ComponentScores { get; set; } = new();
    }

    // Per-component part of a persisted calculation snapshot.
    public class PowerAcKpiComponentScore
    {
        public int Id { get; set; }
        public int KpiResultId { get; set; }
        public PowerAcKpiResult? KpiResult { get; set; }
        public int ComponentId { get; set; }
        public PowerAcComponent? Component { get; set; }
        public decimal? WeightPercent { get; set; }
        // Exact scoring version (method, target, approval) used for this snapshot.
        public int? ComponentVersionId { get; set; }
        public string ScoringMethod { get; set; } = string.Empty;
        public decimal? ApplicableCount { get; set; }
        public decimal? CompletedCount { get; set; }
        public int? IncidentCount { get; set; }
        public decimal? AchievementRatio { get; set; }
        public decimal? Score { get; set; }
        public decimal? WeightedScore { get; set; }
        public string Status { get; set; } = PowerAcResultStatus.Incomplete;
        public string? Message { get; set; }
    }

    public static class PowerAcComponentCodes
    {
        public const string PreventiveMaintenance = "PM";
        public const string CriticalSafety = "SAFETY";
        public const string CorrectiveAction = "CORRECTIVE";
        public const string PowerSystemAvailability = "AVAILABILITY";
    }

    public static class PowerAcDataSources
    {
        public const string PowerAndAC = "PowerAndAC";
        public const string ComponentResult = "ComponentResult";
    }

    public static class PowerAcScoringMethods
    {
        // Not configured yet. The component cannot be scored.
        public const string Pending = "Pending";
        // min(completed / applicable, 1). Over-achievement is capped and reported as a warning.
        public const string RatioCapped = "RatioCapped";
        // completed / applicable. Completed above applicable makes the component incomplete.
        public const string RatioStrict = "RatioStrict";
        // min((completed / applicable) / (target / 100), 1).
        public const string TargetScaled = "TargetScaled";
        // min(achievementPercent / 100, 1) using the entered percentage.
        public const string DirectPercent = "DirectPercent";
        // 1 when the incident count is zero, otherwise 0.
        public const string ZeroIncidentsBinary = "ZeroIncidentsBinary";
        // max(0, 1 - incidents x ScoringParameter / 100).
        public const string DeductionPerIncident = "DeductionPerIncident";

        public static readonly string[] All =
        {
            Pending, RatioCapped, RatioStrict, TargetScaled, DirectPercent, ZeroIncidentsBinary, DeductionPerIncident
        };

        public static readonly string[] RatioMethods = { RatioCapped, RatioStrict, TargetScaled };
        public static readonly string[] IncidentMethods = { ZeroIncidentsBinary, DeductionPerIncident };
    }

    public static class PowerAcPointsMethods
    {
        // Workbook 'KPI Calculation'!K19: IF(composite > target, max, max x composite / target).
        public const string TargetScaled = "TargetScaled";
        // max x composite.
        public const string Linear = "Linear";

        public static readonly string[] All = { TargetScaled, Linear };
    }

    public static class PowerAcRecordStatus
    {
        public const string Draft = "Draft";
        public const string Submitted = "Submitted";
        public const string Verified = "Verified";
        public const string Rejected = "Rejected";
    }

    public static class PowerAcResultStatus
    {
        public const string Complete = "Complete";
        public const string Incomplete = "Incomplete";
        public const string Pending = "Pending";
    }

    public static class PowerAcPeriodTypes
    {
        public const string Month = "Month";
        public const string Quarter = "Quarter";
    }
}
