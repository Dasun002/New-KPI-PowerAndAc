/*
 * File: BbAnwDto.cs
 * Data Transfer Object representing a BB&ANW KPI record along with its
 * associated node metrics for API responses.
 */

using System.Collections.Generic;

namespace backend.DTOs
{
    // =========================================================
    // BB&ANW KPI DTO
    // Used to transfer BB&ANW KPI data between backend and frontend
    // =========================================================
    public class BbAnwDto
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

        // Collection of node-level metrics related to this KPI
        public List<BbAnwNodeDto> Nodes { get; set; } = new();
    }
}