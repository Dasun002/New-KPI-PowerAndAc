using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class Telemetry
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Designation { get; set; } = null!;

        public int Year { get; set; }

        public int Month { get; set; }

        public decimal Percentage { get; set; }

        public short? Node_Count { get; set; }
    }
}
