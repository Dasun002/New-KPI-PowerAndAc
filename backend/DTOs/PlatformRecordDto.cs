namespace backend.DTOs
{
    // Represents one month of platform maintenance data indexed by designation.
    public class PlatformRecordDto
    {


        // Normalized month name used to group source rows consistently.
        public string Month { get; set; } = string.Empty;
        // Designation-to-detail lookup returned to the platform pages.
        public Dictionary<string, PlatformDetailDto> Data { get; set; } = new();


    }
}
