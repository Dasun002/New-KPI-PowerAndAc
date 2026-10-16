namespace backend.DTOs
{
    // Represents one Power and AC maintenance source row and its cumulative values.
    public class PowerAndACDto
    {
        // Source row identity, designation, and reporting period.
        public int Id { get; set; }
        public string Designation { get; set; } = null!;
        public int Year { get; set; }
        public int Month { get; set; }
        // Raw scheduled and attended counts.
        public int Scheduled { get; set; }
        public int Attended { get; set; }
        // Cumulative counts used by routine-maintenance calculations.
        public int Cumulative_Sched { get; set; }
        public int Cumulative_Achieved { get; set; }
        // Indicates whether the source row has been verified.
        public bool IsVerified { get; set; }
    }
}
