/*
 * File: PowerAcVersioning.cs
 * Effective-dated scoring versions and the audit log for the Power & AC KPI.
 * Tables are created by database/power-ac/002_power_ac_versions_audit_deploy.sql.
 */

namespace backend.Models.PowerAc
{
    // Scoring configuration of one component, effective from a month. The latest version on or before
    // a period's last month is used for that period, so later changes never alter earlier periods.
    public class PowerAcComponentVersion
    {
        public int Id { get; set; }
        public int ComponentId { get; set; }
        public PowerAcComponent? Component { get; set; }
        public short EffectiveYear { get; set; }
        public byte EffectiveMonth { get; set; }
        public decimal TargetValue { get; set; }
        public string ScoringMethod { get; set; } = PowerAcScoringMethods.Pending;
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

    // Append-only record of every change to Power & AC configuration and data.
    public class PowerAcAuditLog
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

    public static class PowerAcAuditEntities
    {
        public const string ComponentResult = "ComponentResult";
        public const string AreaAllocation = "AreaAllocation";
        public const string ComponentVersion = "ComponentVersion";
        public const string ComponentWeight = "ComponentWeight";
        public const string CalculationSetting = "CalculationSetting";
        public const string EngineerGroup = "EngineerGroup";
        public const string GroupAssignment = "GroupAssignment";
        public const string KpiCalculation = "KpiCalculation";
    }

    public static class PowerAcAuditActions
    {
        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string Submitted = "Submitted";
        public const string Verified = "Verified";
        public const string Rejected = "Rejected";
        public const string Approved = "Approved";
        public const string ApprovalWithdrawn = "ApprovalWithdrawn";
        public const string Deleted = "Deleted";
        public const string Calculated = "Calculated";
    }

    // Readiness states, from least to most ready. Only ScoreReady groups receive points.
    public static class PowerAcReadinessStates
    {
        public const string NotConfigured = "NotConfigured";
        public const string DataIncomplete = "DataIncomplete";
        public const string AwaitingVerification = "AwaitingVerification";
        public const string AwaitingApproval = "AwaitingApproval";
        public const string ScoreReady = "ScoreReady";
    }
}
