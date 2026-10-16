using System.Text.Json.Serialization;

namespace backend.DTOs
{
    // Response DTO for an Other Operator KPI definition.
    public class OtherOperatorKpiDto
    {
        // Stable JSON names define the contract consumed by the frontend.
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("networkEngineerKpi")]
        public string NetworkEngineerKpi { get; set; } = string.Empty;

        [JsonPropertyName("division")]
        public string? Division { get; set; }

        [JsonPropertyName("kpiPercent")]
        public decimal? KpiPercent { get; set; }
    }
}
