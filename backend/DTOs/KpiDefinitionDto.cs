using System.ComponentModel.DataAnnotations;

namespace backend.DTOs
{
    // Response DTO — returned by GET, POST, PUT
    // Response DTO containing the persisted KPI definition and calculated weightage.
    public class KpiDefinitionDto
    {
        // Definition identity and descriptive KPI fields.
        public int Id { get; set; }
        public string Perspectives { get; set; } = "";
        public string StrategicObjectives { get; set; } = "";
        public string KeyPerformanceIndicators { get; set; } = "";
        public string Unit { get; set; } = "";
        public string DescriptionOfKPI { get; set; } = "";
        // Calculated weightage and point allocation used by KPI scoring.
        public decimal Weightage { get; set; }
        public int PointsApplicable { get; set; }
        public int TotalPoints { get; set; }
        // Optional classification, owner, contact, and audit timestamps.
        public string? Category { get; set; }
        public string? EngineerResponsible { get; set; }
        public string? ContactNo { get; set; }
        public string? CreatedAt { get; set; }
        public string? UpdatedAt { get; set; }
    }

    // Request DTO — used for POST (create) and PUT (update) request bodies
    // Request DTO used for both KPI creation and KPI updates.
    public class UpsertKpiDefinitionDto
    {
        // Required descriptive fields are validated before reaching the service layer.
        [Required, MaxLength(50)]
        public string Perspectives { get; set; } = "";

        [Required, MaxLength(50)]
        public string StrategicObjectives { get; set; } = "";

        [Required, MaxLength(100)]
        public string KeyPerformanceIndicators { get; set; } = "";

        [Required, MaxLength(50)]
        public string Unit { get; set; } = "";

        [Required, MaxLength(50)]
        public string DescriptionOfKPI { get; set; } = "";

        [Range(0, 100000)]
        public int PointsApplicable { get; set; } = 0;

        [Range(1, 1000000)]
        public int? TotalPoints { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(100)]
        public string? EngineerResponsible { get; set; }

        [MaxLength(20)]
        public string? ContactNo { get; set; }
    }
}
