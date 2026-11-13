/*
 * File: PowerAcPmData.cs
 * The single Power & A/C table (dbo.PowerAcPmData), laid out like the other KPI data tables
 * (designation, year, month, scheduled, attended, cumulative, is_verified). The Achievement %, allocation
 * and audit columns were added for the Power & A/C KPI.
 */
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models.PowerAc
{
    [Table("PowerAcPmData", Schema = "dbo")]
    public class PowerAcPmData
    {
        public int Id { get; set; }

        [Column("designation")]
        public string Designation { get; set; } = string.Empty;

        // Text, as in the other KPI tables: "2026" and "November".
        [Column("year")]
        public string Year { get; set; } = string.Empty;

        [Column("month")]
        public string Month { get; set; } = string.Empty;

        [Column("scheduled")]
        public int? Scheduled { get; set; }

        [Column("attended")]
        public int? Attended { get; set; }

        public int Cumulative_Sched { get; set; }

        public int Cumulative_Achieved { get; set; }

        [Column("is_verified")]
        public bool IsVerified { get; set; }

        [Column("pm_achievement")]
        public decimal? PmAchievement { get; set; }

        [Column("safety_achievement")]
        public decimal? SafetyAchievement { get; set; }

        [Column("corrective_achievement")]
        public decimal? CorrectiveAchievement { get; set; }

        [Column("availability_achievement")]
        public decimal? AvailabilityAchievement { get; set; }

        // Achievement % of KPIs other than the four fixed columns: a JSON object {code: value}.
        [Column("achievements_json")]
        public string? AchievementsJson { get; set; }

        [Column("area_allocation")]
        public decimal? AreaAllocation { get; set; }

        [Column("entered_by")]
        public string? EnteredBy { get; set; }

        [Column("verified_by")]
        public string? VerifiedBy { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
