/*
 * File: OtherKpiMetric.cs
 * Entity model representing direct area-level metrics for Other KPI.
 */

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("OtherKpiMetrics")]
    public class OtherKpiMetric
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OtherKpiId { get; set; }

        [Required]
        [MaxLength(50)]
        public string AreaCode { get; set; } = null!;

        [Column(TypeName = "decimal(18,4)")]
        public decimal? KpiValue { get; set; }

        [NotMapped]
        public string? Site
        {
            get => AreaCode;
            set => AreaCode = value ?? string.Empty;
        }

        public int Month { get; set; }
        public int Year { get; set; }

        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(OtherKpiId))]
        public OtherKpi? OtherKpi { get; set; }
    }
}
