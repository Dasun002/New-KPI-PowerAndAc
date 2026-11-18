﻿/*
 * File: MtncRoutine.cs
 * Entity model representing maintenance routine KPI information.
 * Maps to the MtncRoutine table in the database.
 */

namespace backend.Models
{
    // =========================================================
    // MAINTENANCE ROUTINE KPI MODEL
    // Represents maintenance routine KPI details and definitions
    // =========================================================
    public class MtncRoutine
    {
        // Unique identifier for the record
        public int Id { get; set; }

        // Sequential number for the maintenance routine
        public byte No { get; set; }

        // KPI name or description
        public string Kpi { get; set; } = string.Empty;

        // Target value or goal for the KPI
        public string Target { get; set; } = string.Empty;

        // Calculation methodology
        public string Calculation { get; set; } = string.Empty;

        // Platform or system where KPI is measured
        public string Platform { get; set; } = string.Empty;

        // DGM (Deputy General Manager) responsible for the KPI
        public string ResponsibleDGM { get; set; } = string.Empty;

        // Defined OLA (Operating Level Agreement) details
        public string DefinedOLADetails { get; set; } = string.Empty;

        // Data sources for the KPI
        public string DataSources { get; set; } = string.Empty;

        // Timestamp when the record was created
        public string? CreatedAt { get; set; }

        // Timestamp when the record was last updated
        public string? UpdatedAt { get; set; }

        // Version or status field
        public byte V { get; set; }
    }
}
