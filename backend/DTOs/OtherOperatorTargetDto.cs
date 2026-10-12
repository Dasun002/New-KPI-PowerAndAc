using System.Text.Json.Serialization;

namespace backend.DTOs
{
    // Response DTO for an Other Operator KPI target assigned to a reporting period.
    public class OtherOperatorTargetDto
    {
        // Target record and parent KPI identifier.
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("otherOperatorKpiId")]
        public int OtherOperatorKpiId { get; set; }

        // KPI and organizational context returned for display.
        [JsonPropertyName("networkEngineerKpi")]
        public string? NetworkEngineerKpi { get; set; }

        [JsonPropertyName("division")]
        public string? Division { get; set; }

        [JsonPropertyName("section")]
        public string? Section { get; set; }

        // Period to which the target applies.
        [JsonPropertyName("month")]
        public byte Month { get; set; }

        [JsonPropertyName("year")]
        public short Year { get; set; }
    }

    // Request body used to create an Other Operator KPI target.
    public class CreateOtherOperatorTargetDto
    {
        // Parent KPI and target period supplied by the client.
        [JsonPropertyName("otherOperatorKpiId")]
        public int OtherOperatorKpiId { get; set; }

        [JsonPropertyName("section")]
        public string? Section { get; set; }

        [JsonPropertyName("month")]
        public byte Month { get; set; }

        [JsonPropertyName("year")]
        public short Year { get; set; }
    }
}
