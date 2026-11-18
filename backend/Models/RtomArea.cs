/*
 * File: RtomArea.cs
 * Entity model representing RTOM (Regional Transmission Operations Management) area lookup.
 * Maps to the rtom_area_lookup table in the database.
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // RTOM AREA LOOKUP MODEL
    // Reference data for RTOM areas and their display names
    // =========================================================
    [Table("rtom_area_lookup")]
    public class RtomArea
    {
        // Area code identifier (primary key)
        [Key]
        [Column("area_code")]
        public string AreaCode { get; set; }

        // Display name for the area
        [Column("display_name")]
        public string DisplayName { get; set; }
    }
}