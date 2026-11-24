/*
 * File: CreateOtnOp2Dto.cs
 * Data Transfer Object for creating OTN Operations 2 KPI records.
 */

namespace backend.DTOs
{
    // =========================================================
    // CREATE OTN OPERATIONS 2 DTO
    // Used for accepting new OTN Op2 KPI creation requests
    // =========================================================
    public class CreateOtnOp2Dto
    {
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
