namespace backend.DTOs
{
    // Holds raw and cumulative maintenance values for one platform designation.
    public class PlatformDetailDto
    {
        // Cumulative scheduled and achieved values exposed in the platform table columns.
        public object? Column2 { get; set; }
        public object? Column3 { get; set; }
        // Source row identity and verification state.
        public int Id { get; set; }
        public bool IsVerified { get; set; }

        // Raw monthly scheduled and attended values.
        public object? Scheduled { get; set; }
        public object? Attended { get; set; }

    }
}
