/*
 * File: OtherKpi.cs
 * Entity model for Other KPI definitions.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("OtherKpi")]
    public class OtherKpi
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string NetworkEngineerKpi { get; set; } = null!;

        public string? Division { get; set; }

        public string? Section { get; set; }

        [Column(TypeName = "decimal(6,3)")]
        public decimal? KpiPercent { get; set; }
    }
}
