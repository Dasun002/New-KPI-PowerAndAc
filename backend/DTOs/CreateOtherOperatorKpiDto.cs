using System.Text.Json.Serialization;

namespace backend.DTOs
{
    // Request body used to create an Other Operator KPI definition.
    public class CreateOtherOperatorKpiDto
    {
        // KPI name or description supplied by the administrator.
        [JsonPropertyName("networkEngineerKpi")]
        public string NetworkEngineerKpi { get; set; } = string.Empty;

        // Optional division responsible for the KPI.
        [JsonPropertyName("division")]
        public string? Division { get; set; }
    }
}
