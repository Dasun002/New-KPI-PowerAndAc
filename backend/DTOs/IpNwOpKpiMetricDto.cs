/*
 * File: IpNwOpKpiMetricDto.cs
 * Data Transfer Object representing IP Network Operations KPI metrics at area level.
 */

namespace backend.DTOs
{
    // =========================================================
    // IP NETWORK OPERATIONS KPI METRIC DTO
    // Contains area-level metrics for IP Network Operations KPI
    // =========================================================
    public class IpNwOpKpiMetricDto
    {
        // Unique identifier for the metric record
        public int Id { get; set; }

        // Foreign key to the parent IpNwOpKpi record
        public int IpNwOpKpiId { get; set; }

        // Area code identifier
        public string AreaCode { get; set; } = string.Empty;

        // Minutes of unavailability for this area
        public int? UnavailableMinutes { get; set; }

        // Total minutes for the measurement period
        public int? TotalMinutes { get; set; }

        // Total number of nodes in this area
        public int? TotalNodes { get; set; }

        // Month of the data
        public byte Month { get; set; }

        // Year of the data
        public short Year { get; set; }
    }
}