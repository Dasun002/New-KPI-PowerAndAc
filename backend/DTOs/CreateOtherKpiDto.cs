/*
 * File: CreateOtherKpiDto.cs
 * DTO for creating/updating Other KPI records.
 */

using System.Text.Json.Serialization;

namespace backend.DTOs
{
    // Request body used to create or update an Other KPI definition.
    public class CreateOtherKpiDto
    {
        // KPI name and organizational context are serialized with explicit API field names.
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
