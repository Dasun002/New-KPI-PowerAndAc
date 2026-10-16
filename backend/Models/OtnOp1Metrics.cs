/*
 * File: OtnOp1Metrics.cs
 * Entity model representing site-level metrics for OTN Operations 1 KPI.
 * Maps to the OtnOp1Metrics table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // OTN OPERATIONS 1 METRICS MODEL
    // Represents site-level metrics for OTN Operations 1 KPI
    // =========================================================
    [Table("OtnOp1Metrics")]
    public class OtnOp1Metrics
    {
        // Unique identifier for the metric record
        [Key]
        public int Id { get; set; }

        // Foreign key to the parent OtnOp1 record (required)
        [Required]
        public int OtnOp1Id { get; set; }

        // Site identifier or code (required)
        [Required]
        public string Site { get; set; } = null!;

        // Minutes of unavailability at this site
        public int UnavailableMinutes { get; set; }

        // Total minutes for the measurement period
        public int TotalMinutes { get; set; }

        // Total number of nodes at this site
        public int TotalNodes { get; set; }

        // Year of the data
        public short Year { get; set; }

        // Month of the data
        public byte Month { get; set; }

        // Navigation property: reference to parent OtnOp1
        [ForeignKey(nameof(OtnOp1Id))]
        public OtnOp1? OtnOp1 { get; set; }
    }
}
