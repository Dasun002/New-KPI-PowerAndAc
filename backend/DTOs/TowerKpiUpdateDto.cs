/*
 * File: TowerKpiUpdateDto.cs
 * Data Transfer Object for updating Tower KPI records.
 */

using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    // =========================================================
    // UPDATE TOWER KPI DTO
    // Used for accepting Tower KPI update requests
    // =========================================================
    public class TowerKpiUpdateDto
    {
        // Sequential number for the Tower KPI (required)
        [Required]
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
