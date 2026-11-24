/*
 * File: OtnOp1Dto.cs
 * Data Transfer Object representing OTN Operations 1 KPI records.
 */

namespace backend.DTOs
{
    // =========================================================
    // OTN OPERATIONS 1 KPI DTO
    // Used to transfer OTN Op1 KPI data between backend and frontend
    // =========================================================
    public class OtnOp1Dto
    {
        // Unique identifier for the OTN Op1 KPI record
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
