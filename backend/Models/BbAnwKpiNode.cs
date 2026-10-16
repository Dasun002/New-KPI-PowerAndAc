/*
 * File: BbAnwKpiNode.cs
 * Entity model representing node-level metrics for BB&ANW KPI data.
 * Maps to the BbAnwKpiNode table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace backend.Models
{
    // =========================================================
    // BB&ANW KPI NODE MODEL
    // Represents node-specific metrics for a BB&ANW KPI
    // =========================================================
    [Table("BbAnwKpiNode", Schema = "dbo")]
    public class BbAnwKpiNode
    {
        // Unique identifier for the node record
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // Foreign key to the parent BbAnwKpi record
        [Column("bb_anw_kpi_id")]
        public int BbAnwKpiId { get; set; }

        // Node identifier or code
        [Column("node_code")]
        public string NodeCode { get; set; } = string.Empty;

        // Minutes of unavailability for this node
        [Column("unavailable_minutes")]
        public int? UnavailableMinutes { get; set; }

        // Total minutes for the measurement period (mapped as bigint in DB)
        [Column("total_minutes")]
        public long? TotalMinutes { get; set; }

        // Total number of nodes
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
        public BbAnwKpi? Kpi { get; set; }
    }
}
