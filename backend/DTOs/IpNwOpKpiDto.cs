/*
 * File: IpNwOpKpiDto.cs
 * Data Transfer Object representing IP Network Operations KPI data with area-wise metrics.
 */

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace backend.DTOs
{
    // =========================================================
    // IP NETWORK OPERATIONS KPI DTO
    // Transfers IP Network Operations KPI data with area-level metrics
    // =========================================================
    public class IpNwOpKpiDto
    {
        // Unique identifier (converted from string to int)
        [JsonPropertyName("id")]
        public int Id { get; set; }

        // Name or description of the Network Engineer KPI
        [JsonPropertyName("network_engineer_kpi")]
        public string? NetworkEngineerKpi { get; set; }

        // Division responsible for the KPI
        [JsonPropertyName("division")]
        public string? Division { get; set; }

        // Section within the division
        [JsonPropertyName("section")]
        public string? Section { get; set; }

        // KPI percentage weight or contribution
        [JsonPropertyName("kpi_percent")]
        public double? KpiPercent { get; set; }

        // Timestamp of last update
        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Area-wise mapping of unavailable minutes
        [JsonPropertyName("unavailable_minutes")]
        public Dictionary<string, int?> UnavailableMinutes { get; set; } = new();

        // Area-wise mapping of total minutes
        [JsonPropertyName("total_minutes")]
        public Dictionary<string, int?> TotalMinutes { get; set; } = new();

        // Area-wise mapping of total nodes
        [JsonPropertyName("total_nodes")]
        public Dictionary<string, int?> TotalNodes { get; set; } = new();
    }
}