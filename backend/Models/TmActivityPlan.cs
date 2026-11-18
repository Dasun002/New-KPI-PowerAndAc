/*
 * File: TmActivityPlan.cs
 * Entity model representing Traffic Management (TM) activity plans.
 * Maps to the tmtable1 table in the database.
 * NOTE: File name is TmActivityPlan.cs but class name is TmActivity1
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // TM ACTIVITY 1 MODEL
    // Represents Traffic Management activity plan details
    // =========================================================
    [Table("tmtable1", Schema = "dbo")]
    public class TmActivity1
    {
        // Unique identifier for the activity plan
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Sequential number for the activity (optional)
        public byte? No { get; set; }

        // KPI name or description (required)
        [Required]
        public string Kpi { get; set; } = null!;

        // Target value or goal (optional)
        public string? Target { get; set; }

        // Calculation methodology (optional)
        public string? Calculation { get; set; }

        // Timestamp when the activity plan was created (optional, stored as text)
        public string? CreatedAt { get; set; }

        // Timestamp when the activity plan was last updated (optional, stored as text)
        public string? UpdatedAt { get; set; }
    }
}
