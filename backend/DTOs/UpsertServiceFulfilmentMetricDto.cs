/*
 * File: UpsertServiceFulfilmentMetricDto.cs
 * Data Transfer Object for creating or updating service fulfillment metrics.
 */

namespace backend.DTOs
{
    // =========================================================
    // UPSERT SERVICE FULFILLMENT METRIC DTO
    // Used for both creating and updating service fulfillment metrics
    // =========================================================
    public class UpsertServiceFulfilmentMetricDto
    {
        // Foreign key to the Service Fulfillment KPI
        public int ServiceFulfilmentKpiId { get; set; }

        // Area code identifier
        public string AreaCode { get; set; } = string.Empty;

        // KPI value achieved (optional)
        public decimal? KpiValue { get; set; }

        // Month for which this metric applies
        public byte Month { get; set; }

        // Year for which this metric applies
        public short Year { get; set; }
    }
}
