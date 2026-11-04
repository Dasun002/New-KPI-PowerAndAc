using System.ComponentModel.DataAnnotations;

namespace backend.DTOs
{
    public class PowerAndACWriteDto
    {
        [Required]
        [StringLength(100)]
        public string Designation { get; set; } = string.Empty;

        [Range(2000, 9999)]
        public int Year { get; set; }

        [Range(1, 12)]
        public int Month { get; set; }

        [Range(0, int.MaxValue)]
        public int Scheduled { get; set; }

        [Range(0, int.MaxValue)]
        public int Attended { get; set; }

        [Range(0, int.MaxValue)]
        public int Cumulative_Sched { get; set; }

        [Range(0, int.MaxValue)]
        public int Cumulative_Achieved { get; set; }
    }
}
