/*
 * File: EnterpriseKpi.cs
 * Entity model for Enterprise KPI definitions.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("EnterpriseKpi")]
    public class EnterpriseKpi
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string NetworkEngineerKpi { get; set; } = null!;

        public string? Division { get; set; }

        [Column(TypeName = "decimal(6,3)")]
        public decimal? KpiPercent { get; set; }
    }
}
