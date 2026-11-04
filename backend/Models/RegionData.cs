/*
 * File: RegionData.cs
 * Entity model representing region and area information.
 * Maps to the regiondata table in the database.
 */

using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    // =========================================================
    // REGION DATA MODEL
    // Represents geographic region and area information
    // =========================================================
    [Table("regiondata")]
    public class RegionData
    {
        // Unique identifier for the region record
        [Column("id")]
        public int Id { get; set; }

        // Region identifier or name
        [Column("region")]
        public string Region { get; set; }

        // Province or state within the region
        [Column("province")]
        public string Province { get; set; }

        // Network engineer responsible for the region
        [Column("network_engineer")]
        public string NetworkEngineer { get; set; }

        // Engineer Name
        [Column("EngName")]
        public string? EngName { get; set; }


        // LEA (Link, Equipment, Access) code
        [Column("lea_code")]
        public string LeaCode { get; set; }
    }
}