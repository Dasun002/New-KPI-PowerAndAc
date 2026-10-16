using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("slbnmtcdata", Schema = "dbo")]
    public class SlbnMtcData
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("designation")]
        public string? Designation { get; set; }

        [Column("year")]
        public int? Year { get; set; }

        [Column("month", TypeName = "varchar(10)")]
        public string? Month { get; set; }

        [Column("scheduled")]
        public int? Scheduled { get; set; }

        [Column("attended")]
        public int? Attended { get; set; }

        [Column("Cumulative_Sched")]
        public int CumulativeSched { get; set; }

        [Column("Cumulative_Achieved")]
        public int CumulativeAchieved { get; set; }

        [Column("is_verified")]
        public bool IsVerified { get; set; } = false;
    }
}