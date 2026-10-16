namespace backend.DTOs
{
    // Represents a telemetry percentage and its resolved regional display context.
    public class TelemetryDto
    {
        // Source telemetry identity, designation, period, and measured percentage.
        public int Id { get; set; }
        public string Designation { get; set; } = null!;
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Percentage { get; set; }
        public short? Node_Count { get; set; }

        // RegionData values joined to the telemetry designation for regional display.
        public string Region { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string NetworkEngineer { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
    }
}
