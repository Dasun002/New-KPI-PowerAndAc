namespace backend.DTOs
{
    // Compact IPNW maintenance KPI row used by platform summary responses.
    public class IpnwKpiDto
    {
        // Source designation and scheduled work total.
        public string Designation { get; set; }
        public int Scheduled { get; set; }
        // Calculated achievement value for the designation.
        public decimal Achievement { get; set; }
    }
}
