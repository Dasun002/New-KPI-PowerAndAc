using System.Text.Json.Serialization;

namespace backend.DTOs
{
    // Represents one site-level result recorded for an Other Operator KPI.
    public class OtherOperatorKpiMetricDto
    {
        // Metric and parent KPI identifiers; OtherKpiId is retained as a compatibility alias.
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("otherOperatorKpiId")]
        public int OtherOperatorKpiId { get; set; }

        [JsonPropertyName("otherKpiId")]
        public int OtherKpiId
        {
            get => OtherOperatorKpiId;
            set => OtherOperatorKpiId = value;
        }

        // KPI and organizational context returned with the metric.
        [JsonPropertyName("networkEngineerKpi")]
        public string? NetworkEngineerKpi { get; set; }

        [JsonPropertyName("division")]
        public string? Division { get; set; }

        // Site measurement, value, target, and contribution percentage.
        [JsonPropertyName("site")]
        public string? Site { get; set; }

        [JsonPropertyName("kpiValue")]
        public decimal? KpiValue { get; set; }

        [JsonPropertyName("target")]
        public string? Target { get; set; }

        [JsonPropertyName("kpiPercent")]
        public decimal? KpiPercent { get; set; }

        // Reporting period for the metric.
        [JsonPropertyName("month")]
        public byte Month { get; set; }

        [JsonPropertyName("year")]
        public short Year { get; set; }
    }
}
