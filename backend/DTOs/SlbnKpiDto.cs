namespace backend.DTOs
{
    // Compact SLBN maintenance KPI row used by platform summary responses.
    public class SlbnKpiDto
    {
        // Source designation and scheduled work total.
        public string Designation { get; set; }
        public int Scheduled { get; set; }
        // Calculated achievement value for the designation.
        public decimal Achievement { get; set; }
    }
}
