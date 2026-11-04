namespace backend.DTOs
{
    // Represents an analytics aggregation for one KPI definition and area over a selected year.
    public class AnalyticsResultDto
    {
        // Identifies the KPI definition represented by this aggregation.
        public int KpiDefinitionId { get; set; }
        // Display name of the KPI.
        public string? KpiName { get; set; }
        // Area whose KPI values were aggregated.
        public string AreaCode { get; set; } = string.Empty;
        // Averaged achieved KPI value across the selected months.
        public decimal AchievedKpi { get; set; }
        // Averaged maximum points available for the KPI.
        public decimal MaximumPointsPerKpi { get; set; }
        // Averaged points achieved across the selected months.
        public decimal PointsAchieved { get; set; }
        // Combined KPI percentage for all calculated KPIs in the area.
        public decimal OverallKpiValuePercent { get; set; }
        // Year covered by the analytics request.
        public short Year { get; set; }
    }
}
