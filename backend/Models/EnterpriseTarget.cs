using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("EnterpriseTargets")]
    public class EnterpriseTarget
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EnterpriseKpiId { get; set; }

        [MaxLength(100)]
        public string? Section { get; set; }

        public byte Month { get; set; }

        public short Year { get; set; }

        [ForeignKey(nameof(EnterpriseKpiId))]
        public EnterpriseKpi? EnterpriseKpi { get; set; }
    }
}
