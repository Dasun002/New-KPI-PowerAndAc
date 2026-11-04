/*
 * File: KpiDefinition.cs
 * Entity model representing KPI definitions with perspectives and weightage.
 * Maps to the finaldatatables table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // KPI DEFINITION MODEL
    // Represents a KPI definition with strategic perspective and weightage
    // =========================================================
    [Table("finaldatatables", Schema = "dbo")]
    public class KpiDefinition
    {
        // Unique identifier for the KPI definition
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Balanced scorecard perspectives
        [Column("perspectives")]
        public string Perspectives { get; set; } = string.Empty;

        // Strategic objectives related to the KPI
        [Column("strategicObjectives")]
        public string StrategicObjectives { get; set; } = string.Empty;

        // Key performance indicators description
        [Column("keyPerformanceIndicators")]
        public string KeyPerformanceIndicators { get; set; } = string.Empty;

        // Unit of measurement for the KPI
        [Column("unit")]
        public string Unit { get; set; } = string.Empty;

        // Detailed description of the KPI
        [Column("descriptionOfKPI")]
        public string DescriptionOfKPI { get; set; } = string.Empty;

        // Weightage percentage for the KPI (decimal with 4 decimal places)
        [Column("weightage", TypeName = "decimal(10,4)")]
        public decimal Weightage { get; set; } = 0m;

        // Maximum points applicable for this KPI
        [Column("pointsApplicable")]
        public int PointsApplicable { get; set; } = 0;

        // Total points used for weightage calculation
        [Column("totalPoints")]
        public int TotalPoints { get; set; } = 36000;

        // Optional category for the KPI
        [Column("category")]
        [MaxLength(50)]
        public string? Category { get; set; } = string.Empty;

        // Engineer responsible for the KPI
        [Column("engineerResponsible")]
        [MaxLength(100)]
        public string? EngineerResponsible { get; set; } = string.Empty;

        // Contact number for the KPI
        [Column("contactNo")]
        [MaxLength(20)]
        public string? ContactNo { get; set; } = string.Empty;

        // Timestamp when the KPI definition was created
        [Column("createdAt")]
        public string? CreatedAt { get; set; }

        // Timestamp when the KPI definition was last updated
        [Column("updatedAt")]
        public string? UpdatedAt { get; set; }

    }
}
