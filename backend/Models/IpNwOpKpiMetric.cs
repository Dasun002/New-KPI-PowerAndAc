/*
 * File: IpNwOpKpiMetric.cs
 * Entity model representing area-level metrics for IP Network Operations KPI.
 * Maps to the IpNwOpKpiMetrics table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace backend.Models
{
    // =========================================================
    // IP NETWORK OPERATIONS KPI METRIC MODEL
    // Represents area-level metrics for IP Network Operations KPI
    // =========================================================
    [Table("IpNwOpKpiMetrics", Schema = "dbo")]
    public class IpNwOpKpiMetric
    {
        // Unique identifier for the metric record
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // Foreign key to the parent IpNwOpKpi record
        [Column("ip_nw_op_kpi_id")]
        public int IpNwOpKpiId { get; set; }

        // Area code identifier
        [Column("area_code")]
        public string AreaCode { get; set; } = string.Empty;

        // Minutes of unavailability for this area
        [Column("unavailable_minutes")]
        public int? UnavailableMinutes { get; set; }

        // Total minutes for the measurement period
        [Column("total_minutes")]
        public int? TotalMinutes { get; set; }

        // Total number of nodes in this area
        [Column("total_nodes")]
        public int? TotalNodes { get; set; }

        // Month of the data
        [Column("month")]
        public byte Month { get; set; }

        // Year of the data
        [Column("year")]
        public short Year { get; set; }

        // Navigation property: reference to parent KPI
        [JsonIgnore]
        [ForeignKey(nameof(IpNwOpKpiId))]
        public IpNwOpKpi IpNwOpKpi { get; set; } = null!;
    }
}