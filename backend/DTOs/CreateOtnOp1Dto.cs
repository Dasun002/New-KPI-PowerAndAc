/*
 * File: CreateOtnOp1Dto.cs
 * Data Transfer Object for creating OTN Operations 1 KPI records.
 */

namespace backend.DTOs
{
    // =========================================================
    // CREATE OTN OPERATIONS 1 DTO
    // Used for accepting new OTN Op1 KPI creation requests
    // =========================================================
    public class CreateOtnOp1Dto
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
