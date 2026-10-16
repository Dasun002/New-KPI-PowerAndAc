using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("OtherOperatorKpiMetrics")]
    public class OtherOperatorKpiMetric
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OtherOperatorKpiId { get; set; }

        public string? Site { get; set; }

        public short Year { get; set; }

        public byte Month { get; set; }

        [Column("kpi_value", TypeName = "decimal(18,4)")]
        public decimal? KpiValue { get; set; }

        public string? Target { get; set; }

        [ForeignKey(nameof(OtherOperatorKpiId))]
        public OtherOperatorKpi? OtherOperatorKpi { get; set; }
    }
}
