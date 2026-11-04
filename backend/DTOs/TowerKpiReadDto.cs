/*
 * File: TowerKpiReadDto.cs
 * Data Transfer Object for reading Tower KPI records.
 */

namespace backend.Dtos
{
    // =========================================================
    // READ TOWER KPI DTO
    // Used for returning Tower KPI data from the API
    // =========================================================
    public class TowerKpiReadDto
    {
        // Sequential number for the Tower KPI
        public byte No { get; set; }

        // Responsibility description
        public string Responsibility { get; set; } = string.Empty;

        // Frequency of measurement or review
        public string Frequency { get; set; } = string.Empty;

        // Weightage percentage for the KPI
        public string Weightage { get; set; } = string.Empty;

        // KPI name or description
        public string Kpi { get; set; } = string.Empty;
    }
}

