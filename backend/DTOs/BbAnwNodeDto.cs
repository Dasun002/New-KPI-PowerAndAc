/*
 * File: BbAnwNodeDto.cs
 * Data Transfer Object representing node-level metrics for BB&ANW KPI data.
 */

namespace backend.DTOs
{
    // =========================================================
    // BB&ANW NODE METRICS DTO
    // Contains node-specific metrics for BB&ANW KPI
    // =========================================================
    public class BbAnwNodeDto
    {
        // Node identifier or code
        public string NodeCode { get; set; } = string.Empty;

        // Minutes of unavailability for the node
        public int? UnavailableMinutes { get; set; }

        // Total minutes for the measurement period
        public long? TotalMinutes { get; set; }

        // Total number of nodes
        public int? TotalNodes { get; set; }

        // Month of the data
        public byte Month { get; set; }

        // Year of the data
        public short Year { get; set; }
    }
}
