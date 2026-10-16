// DTOs for IPNW maintenance rows and validated create/update requests.
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs
{
    // Response DTO containing raw and two-month cumulative IPNW maintenance values.
    public class IpnwMtcDataDto
    {
        // Source row identity, designation, and reporting period.
        public int Id { get; set; }
        public string Designation { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Month { get; set; } = string.Empty;
        // Raw monthly scheduled and attended counts.
        public int Scheduled { get; set; }
        public int Attended { get; set; }
        // Cumulative counts maintained by the IPNW backfill service.
        public int CumulativeSched { get; set; }
        public int CumulativeAchieved { get; set; }
        // Verification state returned to the maintenance UI.
        public bool IsVerified { get; set; }
    }

    // Request DTO for inserting or updating raw IPNW maintenance counts.
    public class UpsertIpnwMtcDataDto
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

