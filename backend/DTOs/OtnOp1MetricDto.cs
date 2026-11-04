/*
 * File: OtnOp1MetricDto.cs
 * Data Transfer Object representing OTN Operations 1 KPI metrics at site level.
 */

namespace backend.DTOs
{
    // =========================================================
    // OTN OPERATIONS 1 KPI METRIC DTO
    // Contains site-level metrics for OTN Operations 1 KPI
    // =========================================================
    public class OtnOp1MetricDto
    {
        // Unique identifier for the metric record (optional in responses)
        public int Id { get; set; }

        // Foreign key to the parent OtnOp1 record
        public int OtnOp1Id { get; set; }

        // Site identifier or code
        public string Site { get; set; } = string.Empty;

        // Minutes of unavailability for this site
        public int UnavailableMinutes { get; set; }

        // Total minutes for the measurement period
        public int TotalMinutes { get; set; }

        // Total number of nodes at this site
        public int TotalNodes { get; set; }

        // Year of the data
        public short Year { get; set; }

        // Month of the data
        public byte Month { get; set; }
    }
}
