/*
 * File: OtnOp2Metrics.cs
 * Entity model representing site-level metrics for OTN Operations 2 KPI.
 * Maps to the OtnOp2Metrics table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // OTN OPERATIONS 2 METRICS MODEL
    // Represents site-level metrics for OTN Operations 2 KPI
    // =========================================================
    [Table("OtnOp2Metrics")]
    public class OtnOp2Metrics
    {
        // Unique identifier for the metric record
        [Key]
        public int Id { get; set; }

        // Foreign key to the parent OtnOp2 record (required)
        [Required]
        public int OtnOp2Id { get; set; }

        // Site identifier or code (required)
        [Required]
        public string Site { get; set; } = null!;

        // Total number of failed links at this site
        public int TotalFailedLinks { get; set; }

        // Number of links where SLA was not violated
        public int LinksSlaNotViolated { get; set; }

        // Year of the data
        public short Year { get; set; }

        // Month of the data
        public byte Month { get; set; }

        // Navigation property: reference to parent OtnOp2
        [ForeignKey(nameof(OtnOp2Id))]
        public OtnOp2? OtnOp2 { get; set; }
    }
}
