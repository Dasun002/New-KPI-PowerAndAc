using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("OtherOperatorTargets")]
    public class OtherOperatorTarget
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OtherOperatorKpiId { get; set; }

        public string? Section { get; set; }

        public byte Month { get; set; }

        public short Year { get; set; }

        [ForeignKey(nameof(OtherOperatorKpiId))]
        public OtherOperatorKpi? OtherOperatorKpi { get; set; }
    }
}
