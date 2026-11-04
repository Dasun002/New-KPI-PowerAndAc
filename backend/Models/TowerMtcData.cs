using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace backend.Models
{
    [Table("towermtcdata", Schema = "dbo")]
    public class TowerMtcData
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("designation")]
        public string? Designation { get; set; }

        [Column("year")]
        public int? Year { get; set; }

        [Column("month", TypeName = "nchar(10)")]
        public string? Month { get; set; }

        [Column("scheduled")]
        public int? Scheduled { get; set; }

        [Column("attended")]
        public int? Attended { get; set; }

        [Column("Cumulative_Scheduled")]
        public int CumulativeScheduled { get; set; }

        [Column("Cumulative_Achieved")]
        public int CumulativeAttended { get; set; }

        [Column("is_verified")]
        public bool IsVerified { get; set; } = false;
    }
}
