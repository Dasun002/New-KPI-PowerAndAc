/*
 * File: PivotDto.cs
 * Data Transfer Object representing one month of pivoted platform data.
 */

using backend.Services;
using System.Collections.Generic;

namespace backend.DTOs
{
    // Groups platform details by normalized month for pivot-style API responses.
    public class PivotDto
    {
        // Month represented by this pivot row.
        public string Month { get; set; }
        // Designation-to-detail lookup for the month.
        public Dictionary<string, PlatformDetailDto> Data { get; set; }
    }

}
