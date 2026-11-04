/*
 * File: OverallKpiResultDto.cs
 * Data Transfer Object representing overall KPI results with achieved values
 * and points for a specific area and period.
 */

namespace backend.DTOs
{
    // =========================================================
    // OVERALL KPI RESULT DTO
    // Contains overall KPI results and achievements for areas
    // =========================================================
    public class OverallKpiResultDto
    {
        // Unique identifier for the KPI result
        public int Id { get; set; }

        // Foreign key to the KPI definition
        public int KpiDefinitionId { get; set; }

        // Name of the KPI (for reference)
        public string? KpiName { get; set; }

        // Area code for which this result is recorded
        public string AreaCode { get; set; } = string.Empty;

        // KPI value achieved (as a percentage or absolute value)
        public decimal AchievedKpi { get; set; }

        // Maximum points available for this KPI
        public decimal MaximumPointsPerKpi { get; set; }

        // Points achieved based on the KPI performance
        public decimal PointsAchieved { get; set; }

        // Overall KPI value expressed as a percentage
        public decimal OverallKpiValuePercent { get; set; }

        // Month for which this result is recorded
        public byte Month { get; set; }

        // Year for which this result is recorded
        public short Year { get; set; }
    }
}
