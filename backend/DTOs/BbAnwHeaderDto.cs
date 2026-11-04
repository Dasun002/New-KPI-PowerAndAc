/*
 * File: BbAnwHeaderDto.cs
 * Data Transfer Object representing a BB&ANW KPI header record
 * used for API responses and frontend communication.
 */

namespace backend.DTOs
{
    // =========================================================
    // BB&ANW KPI HEADER DTO
    // Contains header information for BB&ANW KPI data
    // =========================================================
    public class BbAnwHeaderDto
    {
        // Unique identifier of the KPI
        public int Id { get; set; }

        // Name or description of the Network Engineer KPI
        public string NetworkEngineerKpi { get; set; } = string.Empty;

        // Division responsible for the KPI
        public string? Division { get; set; }

        // Section within the division
        public string? Section { get; set; }

        // KPI percentage weight or contribution
        public decimal? KpiPercent { get; set; }
    }
}
