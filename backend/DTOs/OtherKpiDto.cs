/*
 * File: OtherKpiDto.cs
 * DTO for Other KPI records.
 */

using System.Text.Json.Serialization;

namespace backend.DTOs
{
    // Response DTO for an Other KPI definition.
    public class OtherKpiDto
    {
        // Definition identity, name, organizational context, and contribution percentage.
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("networkEngineerKpi")]
        public string NetworkEngineerKpi { get; set; } = string.Empty;

        [JsonPropertyName("division")]
        public string? Division { get; set; }

        [JsonPropertyName("section")]
        public string? Section { get; set; }

        [JsonPropertyName("kpiPercent")]
        public decimal? KpiPercent { get; set; }
    }
}
