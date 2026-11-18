using System.Text.Json.Serialization;

namespace backend.DTOs
{
    // Response DTO for an Enterprise KPI target assigned to a reporting period.
    public class EnterpriseTargetDto
    {
        // Target record and parent KPI identifiers.
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("enterpriseKpiId")]
        public int EnterpriseKpiId { get; set; }

        // KPI display and organizational context copied for client rendering.
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

    // Request body used to create an Enterprise KPI target for a reporting period.
    public class CreateEnterpriseTargetDto
    {
        // Parent KPI and target period supplied by the client.
        [JsonPropertyName("enterpriseKpiId")]
        public int EnterpriseKpiId { get; set; }

        [JsonPropertyName("section")]
        public string? Section { get; set; }

        [JsonPropertyName("month")]
        public byte Month { get; set; }

        [JsonPropertyName("year")]
        public short Year { get; set; }
    }
}
