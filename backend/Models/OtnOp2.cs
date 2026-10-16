/*
 * File: OtnOp2.cs
 * Entity model representing OTN Operations 2 KPI data.
 * Maps to the OtnOp2 table in the database.
 */

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // OTN OPERATIONS 2 KPI MODEL
    // Represents an OTN Operations 2 KPI with associated metrics
    // =========================================================
    [Table("OtnOp2")]
    public class OtnOp2
    {
        // Unique identifier for the KPI
        [Key]
        public int Id { get; set; }

        // Name or description of the Network Engineer KPI (required)
        [Required]
        public string NetworkEngineerKpi { get; set; } = null!;

        // Division responsible for the KPI
        public string? Division { get; set; }

        // Section within the division
        public string? Section { get; set; }

        // KPI percentage weight or contribution
        [Column(TypeName = "decimal(6,3)")]
        public decimal? KpiPercent { get; set; }

        // Navigation property: collection of site-level metrics
        public ICollection<OtnOp2Metrics> Metrics { get; set; } = new List<OtnOp2Metrics>();
    }
}
