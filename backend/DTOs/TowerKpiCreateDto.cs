/*
 * File: TowerKpiCreateDto.cs
 * Data Transfer Object for creating Tower KPI records.
 */

using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    // =========================================================
    // CREATE TOWER KPI DTO
    // Used for accepting new Tower KPI creation requests
    // =========================================================
    public class TowerKpiCreateDto
    {
        // Sequential number for the Tower KPI (required, range 0-255)
        [Required]
        [Range(0, 255)]
        public int No { get; set; }

        // Responsibility description (required)
        [Required]
        public string Responsibility { get; set; } = null!;

        // Frequency of measurement or review (required)
        [Required]
        public string Frequency { get; set; } = null!;

        // Weightage percentage for the KPI (required)
        [Required]
        public string Weightage { get; set; } = null!;

        // KPI name or description (required)
        [Required]
        public string Kpi { get; set; } = null!;

        // Month for which this KPI applies (optional)
        public int? Month { get; set; }

        // Year for which this KPI applies (optional)
        public int? Year { get; set; }
    }
}
