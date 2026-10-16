/*
 * File: ServiceFulfilmentKpiDto.cs
 * Data Transfer Object representing service fulfillment KPI details with
 * weightage and performance metrics.
 */

namespace backend.DTOs
{
    // =========================================================
    // SERVICE FULFILLMENT KPI DTO
    // Contains service fulfillment KPI definitions and metrics
    // =========================================================
    public class ServiceFulfilmentKpiDto
    {
        // Unique identifier for the KPI (nullable for create operations)
        public int? Id { get; set; }

        // KPI name or description
        public string Kpi { get; set; } = string.Empty;

        // Target value or goal for the KPI
        public string Target { get; set; } = string.Empty;

        // Calculation methodology for the KPI
        public string Calculation { get; set; } = string.Empty;

        // Platform or system where KPI is measured
        public string Platform { get; set; } = string.Empty;

        // DGM (Deputy General Manager) responsible for the KPI
        public string ResponsibleDgm { get; set; } = string.Empty;

        // Defined OLA (Operating Level Agreement) details
        public string DefineDoladetails { get; set; } = string.Empty;

        // Weightage percentage for the KPI
        public int Weightage { get; set; }

        // Data sources for the KPI
        public string DataSources { get; set; } = string.Empty;

        // Month for which this KPI applies
        public byte Month { get; set; }

        // Year for which this KPI applies
        public short Year { get; set; }

        // Timestamp when the KPI was last updated (optional)
        public string? UpdatedAt { get; set; }
    }
}
