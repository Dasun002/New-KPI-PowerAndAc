using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace backend.Models
{
    [Table("ipnwmtcdata", Schema = "dbo")]
    public class IpnwMtcData
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("designation")]
        public string? Designation { get; set; }

        [Column("year", TypeName = "varchar(10)")]
        public string? Year { get; set; }   // FIXED

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
