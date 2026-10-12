using System.Text.Json.Serialization;

namespace backend.DTOs
{
    // Represents one site measurement captured for an Enterprise KPI and reporting period.
    public class EnterpriseKpiMetricDto
    {
        // Parent Enterprise KPI definition.
        [JsonPropertyName("enterpriseKpiId")]
        public int EnterpriseKpiId { get; set; }

        // Site or service location where the metric was measured.
        [JsonPropertyName("site")]
        public string Site { get; set; } = string.Empty;

        // Optional area code used to group the site in regional views.
        [JsonPropertyName("areaCode")]
        public string? AreaCode { get; set; }

        // Measured KPI value for the period.
        [JsonPropertyName("kpiValue")]
        public decimal? KpiValue { get; set; }

        // Month and year of the measurement.
        [JsonPropertyName("month")]
        public int Month { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        // Target and percentage contribution associated with the measurement.
        [JsonPropertyName("target")]
        public string? Target { get; set; }

        [JsonPropertyName("kpiPercent")]
        public decimal? KpiPercent { get; set; }
    }
}
