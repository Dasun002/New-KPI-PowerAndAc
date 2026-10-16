/*
 * File: OtnOp2Dto.cs
 * Data Transfer Object representing OTN Operations 2 KPI records.
 */

namespace backend.DTOs
{
    // =========================================================
    // OTN OPERATIONS 2 KPI DTO
    // Used to transfer OTN Op2 KPI data between backend and frontend
    // =========================================================
    public class OtnOp2Dto
    {
        // Unique identifier for the OTN Op2 KPI record
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
