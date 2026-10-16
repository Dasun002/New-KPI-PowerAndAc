/*
 * File: OverallKpiResult.cs
 * Entity model representing overall KPI results with calculated achievements.
 * Maps to the OverallKpiResult table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // OVERALL KPI RESULT MODEL
    // Represents calculated KPI results for areas and periods
    // =========================================================
    [Table("OverallKpiResult", Schema = "dbo")]
    public class OverallKpiResult
    {
        // Unique identifier for the KPI result
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        // KPI code for reference
        [Column("KpiCode")]
        public string? KpiCode { get; set; }

        // Foreign key to the KPI definition
        [Column("KpiDefinitionId")]
        public int KpiDefinitionId { get; set; }

        // Name of the KPI (for reference)
        [Column("KpiName")]
        public string? KpiName { get; set; }

        // Platform identifier
        [Column("Platform")]
        public string? Platform { get; set; }

        // Area code for which this result is recorded
        [Column("AreaCode")]
        public string AreaCode { get; set; } = string.Empty;

        // Target value for the KPI
        [Column("TargetValue", TypeName = "decimal(18,4)")]
        public decimal? TargetValue { get; set; }

        // KPI value achieved
        [Column("AchievedValue", TypeName = "decimal(10,4)")]
        public decimal AchievedKpi { get; set; }

        // Maximum points available for this KPI
        [Column("PointsApplicable", TypeName = "decimal(18,4)")]
        public decimal MaximumPointsPerKpi { get; set; }

        // Points achieved based on KPI performance
        [Column("PointsAchieved", TypeName = "decimal(18,4)")]
        public decimal PointsAchieved { get; set; }

        // Overall KPI value expressed as a percentage
        [Column("OverallKpiValuePercent", TypeName = "decimal(10,4)")]
        public decimal OverallKpiValuePercent { get; set; }

        // Month for which this result is recorded
        [Column("Month")]
        public byte Month { get; set; }

        // Year for which this result is recorded
        [Column("Year")]
        public short Year { get; set; }

        // Timestamp when the result was calculated
        [Column("CalculatedAt")]
        public DateTime? CalculatedAt { get; set; }
    }
}
