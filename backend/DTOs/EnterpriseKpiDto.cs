/*
 * File: EnterpriseKpiDto.cs
 * DTO for Enterprise KPI records.
 */

namespace backend.DTOs
{
    // Response DTO for an Enterprise KPI definition.
    public class EnterpriseKpiDto
    {
        // Definition identity, name, responsible division, and contribution percentage.
        public int Id { get; set; }
        public string NetworkEngineerKpi { get; set; } = string.Empty;
        public string? Division { get; set; }
        public decimal? KpiPercent { get; set; }
    }
}
