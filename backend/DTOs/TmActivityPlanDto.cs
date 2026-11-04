/*
 * File: TmActivityPlanDto.cs
 * Data Transfer Objects for Traffic Management (TM) activity plans.
 * Contains read, create, and update DTOs.
 */

using System;
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs
{
    // =========================================================
    // TM ACTIVITY PLAN DTO (Read)
    // Used for returning TM activity plan data from the API
    // =========================================================
    public class TmActivityPlanDto
    {
        // Unique identifier for the activity plan
        public int Id { get; set; }

        // Sequential number or identifier
        public string? No { get; set; }

        // KPI name or description
        public string? Kpi { get; set; }

        // Target value or goal
        public string? Target { get; set; }

        // Calculation methodology
        public string? Calculation { get; set; }
    }

    // =========================================================
    // CREATE TM ACTIVITY PLAN DTO
    // Used for accepting new activity plan creation requests
    // =========================================================
    public class CreateTmActivityPlanDto
    {
        // Sequential number or identifier (optional)
        public string? No { get; set; }

        // KPI name or description (required)
        [Required]
        public string? Kpi { get; set; }

        // Target value or goal (optional)
        public string? Target { get; set; }

        // Calculation methodology (optional)
        public string? Calculation { get; set; }
    }

    // =========================================================
    // UPDATE TM ACTIVITY PLAN DTO
    // Used for updating existing activity plans (inherits from Create)
    // =========================================================
    public class UpdateTmActivityPlanDto : CreateTmActivityPlanDto
    {
        // Inherits all properties from CreateTmActivityPlanDto
    }
}