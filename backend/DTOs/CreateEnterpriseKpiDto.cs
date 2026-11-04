/*
 * File: CreateEnterpriseKpiDto.cs
 * DTO for creating/updating Enterprise KPI records.
 */

namespace backend.DTOs
{
    // Request body used to create or update an Enterprise KPI definition.
    public class CreateEnterpriseKpiDto
    {
        // KPI name, responsible division, and percentage contribution.
        public string NetworkEngineerKpi { get; set; } = string.Empty;
        public string? Division { get; set; }
        public decimal? KpiPercent { get; set; }
    }
}
