/*
 * File: IpNwOpKpi.cs
 * Entity model representing IP Network Operations KPI data.
 * Maps to the IpNwOpKpi table in the database.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace backend.Models
{
    // =========================================================
    // IP NETWORK OPERATIONS KPI MODEL
    // Represents an IP Network Operations KPI with associated metrics
    // =========================================================
    [Table("IpNwOpKpi", Schema = "dbo")]
    public class IpNwOpKpi
    {
        // Unique identifier for the KPI
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // Name or description of the Network Engineer KPI
        [Column("network_engineer_kpi")]
        public string? NetworkEngineerKpi { get; set; }

        // Division responsible for the KPI
        [Column("division")]
        public string? Division { get; set; }

        // Section within the division
        [Column("section")]
        public string? Section { get; set; }

        // KPI percentage weight or contribution (stored as float in SQL)
        [Column("kpi_percent")]
        public double? KpiPercent { get; set; }

        // Timestamp of last update
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation property: collection of area-level metrics
        [JsonIgnore]
        public ICollection<IpNwOpKpiMetric> Metrics { get; set; } = new List<IpNwOpKpiMetric>();
    }
}