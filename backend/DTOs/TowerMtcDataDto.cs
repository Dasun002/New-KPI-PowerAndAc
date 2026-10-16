using System.ComponentModel.DataAnnotations;

namespace backend.DTOs
{
    // Response DTO for one tower maintenance source row, including stored cumulative values.
    public class TowerMtcDataDto
    {
        // Source row identity, designation, and reporting period.
        public int Id { get; set; }
        public string Designation { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Month { get; set; } = string.Empty;
        // Raw monthly scheduled and attended counts.
        public int Scheduled { get; set; }
        public int Attended { get; set; }
        // Cumulative counts maintained for the tower quarterly cycle.
        public int CumulativeSched { get; set; }
        public int CumulativeAchieved { get; set; }
        // Indicates whether the source row has been verified.
        public bool IsVerified { get; set; }
    }

    // Request DTO for inserting or updating raw tower maintenance counts.
    public class UpsertTowerMtcDataDto
    {
        [Required, MaxLength(100)]
        public string Designation { get; set; } = string.Empty;

        [Range(2000, 2100)]
        public int Year { get; set; }

        [Required, MaxLength(10)]
        public string Month { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int Scheduled { get; set; }

        [Range(0, int.MaxValue)]
        public int Attended { get; set; }
    }
}
