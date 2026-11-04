using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class PowerAndAC
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Designation { get; set; } = null!;

        public int Year { get; set; }

        public int Month { get; set; }

        public int Scheduled { get; set; }

        public int Attended { get; set; }

        public int Cumulative_Sched { get; set; }

        public int Cumulative_Achieved { get; set; }

        public bool IsVerified { get; set; } = false;
    }
}
