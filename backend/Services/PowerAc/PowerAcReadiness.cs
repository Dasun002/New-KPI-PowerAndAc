/*
 * File: PowerAcReadiness.cs
 * Classifies every engineer group for a period into one readiness state, so the UI never shows a
 * score without explaining what is still missing:
 *
 *   NotConfigured        mapping unverified, allocation missing, weights invalid or no settings
 *   DataIncomplete       a required month of component data is missing
 *   AwaitingVerification all data present but some of it is not verified (when verification is required)
 *   AwaitingApproval     data ready but a scoring method or the points formula is not approved
 *   ScoreReady           the calculator produced a Complete result with points
 */
using backend.Models.PowerAc;

namespace backend.Services.PowerAc
{
    public sealed class PowerAcComponentReadiness
    {
        public int ComponentId { get; init; }
        public string Code { get; init; } = string.Empty;
        public bool HasData { get; init; }
        public bool DataComplete { get; init; }
        public bool Verified { get; init; }
        public bool Approved { get; init; }
        public string ScoringMethod { get; init; } = string.Empty;
        public int? VersionId { get; init; }
        public List<string> Missing { get; init; } = new();
    }

    public sealed class PowerAcGroupReadiness
    {
        public int EngineerGroupId { get; init; }
        public string Code { get; init; } = string.Empty;
        public bool MappingVerified { get; init; }
        public bool AllocationEntered { get; init; }
        public string State { get; set; } = PowerAcReadinessStates.NotConfigured;
        public string ResultStatus { get; init; } = string.Empty;
        public List<PowerAcComponentReadiness> Components { get; init; } = new();
        public List<string> Blockers { get; init; } = new();
    }

    public sealed class PowerAcReadinessReport
    {
        public PowerAcPeriod Period { get; init; } = null!;
        public bool SettingsConfigured { get; init; }
        public bool PointsMethodApproved { get; init; }
        public bool RequireVerifiedData { get; init; }
        public bool WeightsValid { get; init; }
        public decimal WeightTotal { get; init; }
        public int AllocationsEntered { get; init; }
        public int GroupCount { get; init; }
        public bool AllocationsComplete { get; init; }
        public int ApprovedComponentCount { get; init; }
        public int ComponentCount { get; init; }
        public List<string> PendingBusinessDecisions { get; init; } = new();
        public List<PowerAcGroupReadiness> Groups { get; init; } = new();
        public Dictionary<string, int> StateCounts { get; init; } = new();
    }

    public static class PowerAcReadiness
    {
        public static PowerAcReadinessReport Build(PowerAcCalculationContext context)
        {
            var settings = context.Settings;
            var output = context.Output;
            var configs = context.ComponentConfigs;
            var approvedComponents = configs.Where(IsApproved).ToList();

            var decisions = new List<string>();
            foreach (var c in configs.Where(c => !IsApproved(c)))
            {
                decisions.Add(c.UnavailableReason
                    ?? (c.ScoringMethod == PowerAcScoringMethods.Pending
                        ? $"{c.Name}: scoring rule not yet provided by the business."
                        : $"{c.Name}: scoring method {c.ScoringMethod} awaits Admin approval."));
            }
            if (context.Setting is null) decisions.Add("No calculation settings are effective for this period.");
            else if (!settings.PointsMethodApproved) decisions.Add("The points formula is not approved.");
            foreach (var g in context.Groups.Where(g => !g.MappingVerified))
                decisions.Add($"Engineer group {g.Code} ({g.WorkbookAllocationCell}): mapping needs business confirmation.");

            var groups = new List<PowerAcGroupReadiness>();
            foreach (var input in context.GroupInputs)
            {
                var result = output.Groups.First(r => r.EngineerGroupId == input.EngineerGroupId);
                var componentStates = configs.Select(c =>
                {
                    input.Measurements.TryGetValue(c.ComponentId, out var m);
                    return new PowerAcComponentReadiness
                    {
                        ComponentId = c.ComponentId,
                        Code = c.Code,
                        HasData = m != null,
                        DataComplete = m?.DataComplete == true,
                        Verified = m?.DataComplete == true && m.Verified,
                        Approved = IsApproved(c),
                        ScoringMethod = c.ScoringMethod,
                        VersionId = c.VersionId,
                        Missing = m is null ? new List<string> { "No data entered for the period." } : m.Issues.ToList()
                    };
                }).ToList();

                var blockers = new List<string>();
                if (!input.MappingVerified) blockers.Add("Engineer group mapping is not verified.");
                if (input.AllocationValue is null) blockers.Add("Area allocation is not entered.");
                if (!output.AllocationsComplete) blockers.Add("The allocation set for all groups is incomplete.");
                if (!output.WeightsValid) blockers.Add("Component weights do not total 100%.");
                if (context.Setting is null) blockers.Add("No calculation settings are effective.");

                string state;
                if (result.Status == PowerAcResultStatus.Complete)
                {
                    state = PowerAcReadinessStates.ScoreReady;
                }
                else if (blockers.Count > 0)
                {
                    state = PowerAcReadinessStates.NotConfigured;
                }
                else if (componentStates.Any(c => !c.DataComplete))
                {
                    state = PowerAcReadinessStates.DataIncomplete;
                    blockers.AddRange(componentStates.Where(c => !c.DataComplete).Select(c => $"{c.Code}: {string.Join(" ", c.Missing)}"));
                }
                else if (settings.RequireVerifiedData && componentStates.Any(c => !c.Verified))
                {
                    state = PowerAcReadinessStates.AwaitingVerification;
                    blockers.AddRange(componentStates.Where(c => !c.Verified).Select(c => $"{c.Code}: data is not verified."));
                }
                else if (componentStates.Any(c => !c.Approved) || !settings.PointsMethodApproved)
                {
                    state = PowerAcReadinessStates.AwaitingApproval;
                    blockers.AddRange(componentStates.Where(c => !c.Approved).Select(c => $"{c.Code}: scoring method is not approved."));
                    if (!settings.PointsMethodApproved) blockers.Add("The points formula is not approved.");
                }
                else
                {
                    // Data, verification and approvals are in place but the calculator still refused (for example a ratio of 0/0).
                    state = PowerAcReadinessStates.DataIncomplete;
                    blockers.AddRange(result.Issues);
                }

                groups.Add(new PowerAcGroupReadiness
                {
                    EngineerGroupId = input.EngineerGroupId,
                    Code = input.Code,
                    MappingVerified = input.MappingVerified,
                    AllocationEntered = input.AllocationValue != null,
                    State = state,
                    ResultStatus = result.Status,
                    Components = componentStates,
                    Blockers = blockers.Distinct().ToList()
                });
            }

            return new PowerAcReadinessReport
            {
                Period = context.Period,
                SettingsConfigured = context.Setting != null,
                PointsMethodApproved = settings.PointsMethodApproved,
                RequireVerifiedData = settings.RequireVerifiedData,
                WeightsValid = output.WeightsValid,
                WeightTotal = output.WeightTotal,
                AllocationsEntered = context.GroupInputs.Count(g => g.AllocationValue != null),
                GroupCount = context.GroupInputs.Count,
                AllocationsComplete = output.AllocationsComplete,
                ApprovedComponentCount = approvedComponents.Count,
                ComponentCount = configs.Count,
                PendingBusinessDecisions = decisions.Distinct().ToList(),
                Groups = groups,
                StateCounts = new[]
                {
                    PowerAcReadinessStates.NotConfigured, PowerAcReadinessStates.DataIncomplete,
                    PowerAcReadinessStates.AwaitingVerification, PowerAcReadinessStates.AwaitingApproval,
                    PowerAcReadinessStates.ScoreReady
                }.ToDictionary(s => s, s => groups.Count(g => g.State == s))
            };
        }

        private static bool IsApproved(PowerAcComponentConfig c) =>
            c.UnavailableReason == null && c.ScoringApproved && c.ScoringMethod != PowerAcScoringMethods.Pending;
    }
}
