/*
 * File: EnterpriseKpiMetric.cs
 * Entity model representing area-level metrics for Enterprise KPI.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("EnterpriseKpiMetrics")]
    public class EnterpriseKpiMetric
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EnterpriseKpiId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Site { get; set; } = null!;

        [NotMapped]
        public string? AreaCode
        {
            get => Site;
            set => Site = value ?? string.Empty;
        }

        public decimal? KpiValue { get; set; }

        public string? Target { get; set; }

        public int Month { get; set; }
        public int Year { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(EnterpriseKpiId))]
        public EnterpriseKpi? EnterpriseKpi { get; set; }
    }
}