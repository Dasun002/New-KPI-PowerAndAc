/*
 * File: BbAnwKpi.cs
 * Entity model representing BB&ANW (Backhaul & Access Network) KPI data.
 * Maps to the BbAnwKpi table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace backend.Models
{
    // =========================================================
    // BB&ANW KPI MODEL
    // Represents a BB&ANW KPI record with associated node metrics
    // =========================================================
    [Table("BbAnwKpi", Schema = "dbo")]
    public class BbAnwKpi
    {
        // Unique identifier for the KPI
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // Name or description of the Network Engineer KPI
        [Column("network_engineer_kpi")]
        public string NetworkEngineerKpi { get; set; } = string.Empty;

        // Division responsible for the KPI
        [Column("division")]
        public string? Division { get; set; }

        // Section within the division
        [Column("section")]
        public string? Section { get; set; }

        // KPI percentage weight or contribution
        [Column("kpi_percent")]
        public decimal? KpiPercent { get; set; }

        // Navigation property: collection of node-level metrics
        public List<BbAnwKpiNode> Nodes { get; set; } = new();
    }
}
