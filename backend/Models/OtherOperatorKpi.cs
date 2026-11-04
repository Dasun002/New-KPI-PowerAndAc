using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("OtherOperatorKpi")]
    public class OtherOperatorKpi
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string NetworkEngineerKpi { get; set; } = null!;

        public string? Division { get; set; }

        [Column("kpi_percent", TypeName = "decimal(6,3)")]
        public decimal? KpiPercent { get; set; }
    }
}
