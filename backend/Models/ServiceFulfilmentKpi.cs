/*
 * File: ServiceFulfilmentKpi.cs
 * Entity model representing Service Fulfillment KPI definitions.
 * Maps to the ServiceFulfilmentKpi table in the database.
 */

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // SERVICE FULFILLMENT KPI MODEL
    // Represents Service Fulfillment KPI definitions with metrics
    // =========================================================
    [Table("ServiceFulfilmentKpi")]
    public class ServiceFulfilmentKpi
    {
        // Unique identifier for the KPI
        [Key]
        public int Id { get; set; }

        // KPI name or description
        public string Kpi { get; set; } = string.Empty;

        // Target value or goal for the KPI
        public string Target { get; set; } = string.Empty;

        // Calculation methodology
        public string Calculation { get; set; } = string.Empty;

        // Platform or system where KPI is measured
        public string Platform { get; set; } = string.Empty;

        // DGM (Deputy General Manager) responsible for the KPI
        public string ResponsibleDgm { get; set; } = string.Empty;

        // Defined OLA (Operating Level Agreement) details
        public string DefineDoladetails { get; set; } = string.Empty;

        // Weightage percentage for the KPI
        public int Weightage { get; set; }

        // Data sources for the KPI
        public string DataSources { get; set; } = string.Empty;

        // Month for which this KPI applies
        public byte Month { get; set; }

        // Year for which this KPI applies
        public short Year { get; set; }

        // Timestamp when the KPI was last updated
        public string UpdatedAt { get; set; } = string.Empty;

        // Navigation property: collection of area-level metrics
        public ICollection<ServiceFulfilmentKpiMetric> Metrics { get; set; }
            = new List<ServiceFulfilmentKpiMetric>();
    }
}
