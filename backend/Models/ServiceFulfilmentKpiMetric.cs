/*
 * File: ServiceFulfilmentKpiMetric.cs
 * Entity model representing area-level metrics for Service Fulfillment KPI.
 * Maps to the ServiceFulfilmentKpiMetrics table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // SERVICE FULFILLMENT KPI METRIC MODEL
    // Represents area-level metrics for Service Fulfillment KPI
    // =========================================================
    [Table("ServiceFulfilmentKpiMetrics")]
    public class ServiceFulfilmentKpiMetric
    {
        // Unique identifier for the metric record
        [Key]
        public int Id { get; set; }

        // Foreign key to the parent Service Fulfillment KPI
        public int ServiceFulfilmentKpiId { get; set; }

        // Area code identifier
        public string AreaCode { get; set; } = string.Empty;

        // KPI value achieved for this area (optional)
        public decimal? KpiValue { get; set; }

        // Month for which this metric applies
        public byte Month { get; set; }

        // Year for which this metric applies
        public short Year { get; set; }

        // Navigation property: reference to parent KPI
        [ForeignKey(nameof(ServiceFulfilmentKpiId))]
        public ServiceFulfilmentKpi ServiceFulfilmentKpi { get; set; } = null!;
    }
}
