﻿/*
 * File: MtncRoutineDto.cs
 * Data Transfer Object representing maintenance routine KPI information.
 */

using System.ComponentModel.DataAnnotations;

namespace backend.DTOs
{
    // =========================================================
    // MAINTENANCE ROUTINE KPI DTO
    // Contains maintenance routine KPI details and definitions
    // =========================================================
    public class MtncRoutineDto
    {
        // Sequential number for the maintenance routine (required, 0-255)
        [Required]
        public byte No { get; set; }

        // KPI name or description (required)
        [Required]
        public string Kpi { get; set; } = string.Empty;

        // Target value or goal for the KPI (required)
        [Required]
        public string Target { get; set; } = string.Empty;

        // Calculation methodology (required)
        [Required]
        public string Calculation { get; set; } = string.Empty;

        // Platform or system where KPI is measured (required)
        [Required]
        public string Platform { get; set; } = string.Empty;

        // DGM (Deputy General Manager) responsible for the KPI (required)
        [Required]
        public string ResponsibleDGM { get; set; } = string.Empty;

        // Defined OLA (Operating Level Agreement) details (required)
        [Required]
        public string DefinedOLADetails { get; set; } = string.Empty;

        // Data sources for the KPI (required)
        [Required]
        public string DataSources { get; set; } = string.Empty;
    }
}
