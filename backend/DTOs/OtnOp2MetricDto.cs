/*
 * File: OtnOp2MetricDto.cs
 * Data Transfer Object representing OTN Operations 2 KPI metrics at site level.
 */

namespace backend.DTOs
{
    // =========================================================
    // OTN OPERATIONS 2 KPI METRIC DTO
    // Contains site-level metrics for OTN Operations 2 KPI
    // =========================================================
    public class OtnOp2MetricDto
    {
        // Unique identifier for the metric record
        public int Id { get; set; }

        // Foreign key to the parent OtnOp2 record
        public int OtnOp2Id { get; set; }

        // Site identifier or code
        public string Site { get; set; } = string.Empty;

        // Total number of failed links at this site
        public int TotalFailedLinks { get; set; }

        // Number of links where SLA was not violated
        public int LinksSlaNotViolated { get; set; }

        // Year of the data
        public short Year { get; set; }

        // Month of the data
        public byte Month { get; set; }
    }
}
