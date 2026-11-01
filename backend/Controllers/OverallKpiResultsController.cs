/*
 * File: OverallKpiResultsController.cs
 * Calculates and retrieves overall KPI results by aggregating data from multiple KPI platforms.
 * Includes logic for matching KPI definitions, calculating availability, scoring, and persisting results.
 */

using System.Text.RegularExpressions;
using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers
{
    // =========================================================
    // OVERALL KPI RESULTS CONTROLLER
    // Handles calculation and retrieval of aggregated KPI results
    // Combines metrics from IP, BB&ANW, OTN Op1/Op2, and Service Fulfilment platforms
    // Persists calculated results for performance and audit purposes
    // =========================================================
    [ApiController]
    [Route("api/overall-kpi-results")]
    [Authorize]
    // Calculates monthly KPI scores from platform inputs and persists area-level overall results.
    public class OverallKpiResultsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly backend.Services.RoutineMaintenanceService _routineService;
        private readonly backend.Services.TowerMaintenanceService _towerService;
        private readonly backend.Services.PowerAndACService _powerService;
        // Opt-in switch: when true, the KPI definition named by PowerAc:OverallKpiDefinitionId is filled from the
        // saved four-component Power & AC snapshot instead of the legacy scheduled/attended branch. Default false.
        private readonly bool _useComponentPowerAcKpi;
        private readonly int? _powerAcKpiDefinitionId;

        public OverallKpiResultsController(
            AppDbContext db,
            backend.Services.RoutineMaintenanceService routineService,
            backend.Services.TowerMaintenanceService towerService,
            backend.Services.PowerAndACService powerService,
            IConfiguration configuration)
        {
            _db = db;
            _routineService = routineService;
            _towerService = towerService;
            _powerService = powerService;
            _useComponentPowerAcKpi = configuration.GetValue<bool>("PowerAc:UseComponentKpiInOverall");
            _powerAcKpiDefinitionId = configuration.GetValue<int?>("PowerAc:OverallKpiDefinitionId");
        }

        // =========================================================
        // GET OVERALL KPI RESULTS
        // Retrieves pre-calculated overall KPI results for a given month and year
        // Returns stored results from OverallKpiResult table without recalculation
        // Parameters: month (1-12), year (e.g., 2025)
        // Returns: List of KPI results with achievements and scores by area
        // =========================================================
        [HttpGet]
        public async Task<ActionResult<List<OverallKpiResultDto>>> GetAll(
            [FromQuery] int? month,
            [FromQuery] int? year)
        {
            var now = DateTime.UtcNow;
            byte m = (byte)(month ?? now.Month);
            short y = (short)(year ?? now.Year);

            var rows = await _db.OverallKpiResults
                .AsNoTracking()
                .Where(x => x.Month == m && x.Year == y)
                .OrderBy(x => x.KpiDefinitionId)
                .ThenBy(x => x.AreaCode)
                .ToListAsync();

            return Ok(rows.Select(ToDto).ToList());
        }

        // =========================================================
        // CALCULATE AND PERSIST OVERALL KPI RESULTS
        // Triggers recalculation of overall KPI results for specified month/year
        // Steps:
        // 1. Load KPI definitions (fallback to latest if not found)
        // 2. Retrieve all metrics from platform-specific tables
        // 3. Load area codes from actual metrics (not RegionData)
        // 4. Match KPI definitions with platform metrics (IP, BB, OTN, SF)
        // 5. Calculate per-area KPI values based on platform calculations
        // 6. Compute overall KPI percentage per area
        // 7. Persist results to database for audit and performance
        // Returns: List of newly calculated KPI results
        // =========================================================
        // Requires an authenticated user (class-level [Authorize]). Previously [AllowAnonymous].
        [HttpPost("calculate")]
        public async Task<ActionResult<List<OverallKpiResultDto>>> Calculate(
            [FromQuery] int? month,
            [FromQuery] int? year)
        {
            var now = DateTime.UtcNow;
            byte m = (byte)(month ?? now.Month);
            short y = (short)(year ?? now.Year);

            var calculated = await CalculateAndPersistAsync(m, y);
            return Ok(calculated.Select(ToDto).ToList());
        }

        // Requires an authenticated user (class-level [Authorize]). Previously [AllowAnonymous].
        [HttpPost("calculate-range")]
        public async Task<ActionResult<List<OverallKpiResultDto>>> CalculateRange(
            [FromQuery] int year,
            [FromQuery] int startMonth,
            [FromQuery] int endMonth)
        {
            if (startMonth < 1 || startMonth > 12 || endMonth < 1 || endMonth > 12 || startMonth > endMonth)
                return BadRequest("Invalid months.");

            var allResults = new List<OverallKpiResultDto>();
            for (byte m = (byte)startMonth; m <= (byte)endMonth; m++)
            {
                var calculated = await CalculateAndPersistAsync(m, (short)year);
                allResults.AddRange(calculated.Select(ToDto));
            }

            return Ok(allResults);
        }

        // =========================================================
        // CORE CALCULATION LOGIC
        // Performs end-to-end KPI aggregation and persistence
        // =========================================================
        private async Task<List<OverallKpiResult>> CalculateAndPersistAsync(byte month, short year)
        {
            Console.WriteLine("month: " + month);
            // =========================================================
            // STEP 1: LOAD KPI DEFINITIONS
            // Fetch master KPI definitions for selected month/year
            // Fallback to latest definitions if none exist for selected period
            // =========================================================
            // KpiDefinition no longer has month/year columns — load all definitions
            var kpis = await _db.KpiDefinitions
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            // =========================================================
            // STEP 2: LOAD ALL PLATFORM METRICS
            // Retrieve raw metrics from all KPI platforms for month/year
            // Metrics are loaded early so area codes can be extracted from actual data
            // This fixes NullReferenceException that occurred when RegionData was empty
            // =========================================================
            // Load metrics first to extract area codes
            var ipMetrics = await _db.IpNwOpKpiMetrics.AsNoTracking()
                .Where(x => x.Month == month && x.Year == year)
                .ToListAsync();

            var bbMetrics = await _db.BbAnwKpiNodes.AsNoTracking()
                .Where(x => x.Month == month && x.Year == year)
                .ToListAsync();

            var otn1Metrics = await _db.OtnOp1Metrics.AsNoTracking()
                .Where(x => x.Month == month && x.Year == year)
                .ToListAsync();

            var otn2Metrics = await _db.OtnOp2Metrics.AsNoTracking()
                .Where(x => x.Month == month && x.Year == year)
                .ToListAsync();

            var sfMetrics = await _db.ServiceFulfilmentKpiMetrics.AsNoTracking()
                .Where(x => x.Month == month && x.Year == year)
                .ToListAsync();

            var entMetrics = await _db.EnterpriseKpiMetrics.AsNoTracking()
                .Where(x => x.Month == month && x.Year == year)
                .ToListAsync();

            var otherMetrics = await _db.OtherOperatorKpiMetrics.AsNoTracking()
                .Where(x => x.Month == month && x.Year == year)
                .ToListAsync();

            var telemetryMetrics = await _db.Telemetry.AsNoTracking()
                .Where(x => x.Month == month && x.Year == year)
                .ToListAsync();

            // =========================================================
            // STEP 3: EXTRACT AREA CODES FROM METRICS
            // Aggregate all unique area codes from actual metric data
            // Different platforms use different field names for areas:
            // - IP: AreaCode
            // - BB&ANW: NodeCode
            // - OTN Op1/Op2: Site
            // - Service Fulfilment: AreaCode
            // =========================================================
            // Retrieve all distinct area codes from metrics (not RegionData)
            var allAreaCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            allAreaCodes.UnionWith(ipMetrics
                .Select(x => x.AreaCode)
                .Where(x => x != null && x.Trim() != string.Empty)
                .Select(x => x.Trim()));

            allAreaCodes.UnionWith(bbMetrics
                .Select(x => x.NodeCode)
                .Where(x => x != null && x.Trim() != string.Empty)
                .Select(x => x.Trim()));

            allAreaCodes.UnionWith(otn1Metrics
                .Select(x => x.Site)
                .Where(x => x != null && x.Trim() != string.Empty)
                .Select(x => x.Trim()));

            allAreaCodes.UnionWith(otn2Metrics
                .Select(x => x.Site)
                .Where(x => x != null && x.Trim() != string.Empty)
                .Select(x => x.Trim()));

            allAreaCodes.UnionWith(sfMetrics
                .Select(x => x.AreaCode ?? string.Empty)
                .Where(x => x.Trim() != string.Empty)
                .Select(x => x.Trim()));

            allAreaCodes.UnionWith(entMetrics
                .Select(x => x.AreaCode)
                .Where(x => x != null && x.Trim() != string.Empty)
                .Select(x => x.Trim()));

            allAreaCodes.UnionWith(otherMetrics
                .Select(x => x.Site ?? string.Empty)
                .Where(x => x.Trim() != string.Empty)
                .Select(x => x.Trim()));

            var dbRegions = await _db.RegionData.AsNoTracking().ToListAsync();
            var officialAreas = dbRegions
                .Select(x => NormalizeArea(string.IsNullOrWhiteSpace(x.LeaCode) ? x.NetworkEngineer : x.LeaCode))
                .Where(x => x != string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Build designation -> area map from region and maintenance tables
            var designationToArea = BuildDesignationToAreaMap(dbRegions, officialAreas);
            var normalizedAreas = officialAreas
                .Concat(allAreaCodes.Select(a => CanonicalizeArea(a, officialAreas)))
                .Concat(designationToArea.Values.Select(a => CanonicalizeArea(a, officialAreas)))
                .Where(x => x != string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var ipnwResults = await _routineService.GetIpnwPercentagesAsync(
                    year, month, designationToArea);

            var slbnResults = await _routineService.GetSlbnPercentagesAsync(
                    year, month, designationToArea);

            var msanResults = await _routineService.GetMsanPercentagesAsync(
                    year, month, designationToArea);

            var towerResults = await _towerService.GetTowerPercentagesAsync(
                    year, month, designationToArea);

            var powerResults = await _powerService.GetPowerAndACPercentagesAsync(
                    (short)year, month, designationToArea);

            Console.WriteLine($"IPNW results count = {ipnwResults.Count}");
            Console.WriteLine($"SLBN results count = {slbnResults.Count}");
            Console.WriteLine($"MSAN results count = {msanResults.Count}");
            Console.WriteLine($"Tower results count = {towerResults.Count}");
            Console.WriteLine($"Power & AC results count = {powerResults.Count}");

            // =========================================================
            // STEP 4: VALIDATE DATA AVAILABILITY
            // Return empty results if no KPI definitions or area codes exist
            // =========================================================
            if (!kpis.Any() || !normalizedAreas.Any())
            {
                var emptyExisting = await _db.OverallKpiResults
                    .Where(x => x.Month == month && x.Year == year)
                    .ToListAsync();
                if (emptyExisting.Any())
                {
                    _db.OverallKpiResults.RemoveRange(emptyExisting);
                    await _db.SaveChangesAsync();
                }
                return new List<OverallKpiResult>();
            }

            // =========================================================
            // STEP 5: LOAD PLATFORM KPI DEFINITIONS
            // Fetch all platform-specific KPI names/codes for matching with master KPIs
            // =========================================================
            var ipKpis = await _db.IpNwOpKpis.AsNoTracking()
                .Select(x => new NamedKpi("ip", x.Id, x.NetworkEngineerKpi ?? string.Empty))
                .ToListAsync();
            var bbKpis = await _db.BbAnwKpis.AsNoTracking()
                .Select(x => new NamedKpi("bb", x.Id, x.NetworkEngineerKpi ?? string.Empty))
                .ToListAsync();
            var otn1Kpis = await _db.OtnOp1.AsNoTracking()
                .Select(x => new NamedKpi("otn1", x.Id, x.NetworkEngineerKpi ?? string.Empty))
                .ToListAsync();
            var otn2Kpis = await _db.OtnOp2.AsNoTracking()
                .Select(x => new NamedKpi("otn2", x.Id, x.NetworkEngineerKpi ?? string.Empty))
                .ToListAsync();
            var sfKpis = await _db.ServiceFulfilmentKpis.AsNoTracking()
                .Select(x => new NamedKpi("sf", x.Id, x.Kpi ?? string.Empty))
                .ToListAsync();

            var entKpis = await _db.EnterpriseKpis.AsNoTracking()
                .Select(x => new NamedKpi("ent", x.Id, x.NetworkEngineerKpi ?? string.Empty))
                .ToListAsync();

            var otherKpis = await _db.OtherOperatorKpis.AsNoTracking()
                .Select(x => new NamedKpi("other", x.Id, x.NetworkEngineerKpi ?? string.Empty))
                .ToListAsync();

            var allNamedKpis = ipKpis
                .Concat(bbKpis)
                .Concat(otn1Kpis)
                .Concat(otn2Kpis)
                .Concat(entKpis)
                .Concat(otherKpis)
                .Concat(sfKpis)
                .ToList();

            var enterpriseTargets = await _db.EnterpriseKpis.AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.KpiPercent ?? 0m);

            var daysInMonth = DateTime.DaysInMonth(year, month);
            var results = new List<OverallKpiResult>();
            var nowUtc = DateTime.UtcNow;

            // =========================================================
            // STEP 6: PROCESS EACH KPI DEFINITION
            // For each master KPI:
            // 1. Match platform KPI using fuzzy text matching
            // 2. Build area snapshots with calculated achieved values
            // 3. Allocate points based on node-weighted or equal-share method
            // 4. Calculate final points achieved using target-based scaling
            // =========================================================
            foreach (var kpi in kpis)
            {
                var sourceFilter = new List<string>();
                var p = kpi.Perspectives?.Trim().ToUpperInvariant() ?? string.Empty;
                var cat = kpi.Category?.Trim().ToUpperInvariant() ?? string.Empty;
                if (p == "IP NW OP" || p.Contains("IP")) sourceFilter.Add("ip");
                if (p == "BB ANW" || p.Contains("BB")) sourceFilter.Add("bb");
                if (p == "OTN OP" || p.Contains("OTN")) { sourceFilter.Add("otn1"); sourceFilter.Add("otn2"); }
                if (p == "SERVICE FULFILMENT" || p.Contains("FULFILMENT")) sourceFilter.Add("sf");
                if (p == "ENTERPRISE KPI" || p.Contains("ENTERPRISE"))
                {
                    if (cat == "OTHER OPERATOR" || cat.Contains("OPERATOR"))
                    {
                        sourceFilter.Add("other");
                    }
                    else
                    {
                        sourceFilter.Add("ent");
                    }
                }
                if (p == "OTHER OPERATOR KPI" || p == "OTHER OPERATOR" || p.Contains("OPERATOR")) sourceFilter.Add("other");

                if (kpi.KeyPerformanceIndicators.Equals("Routine Maintenance - IPNW", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("ENTERED IPNW BLOCK");
                    var totalIpnwNodes = ipnwResults.Sum(x => x.NodesCount);
                    foreach (var record in ipnwResults.Where(x => x.NodesCount > 0m))
                    {
                        var area = record.NormalizedAreaCode;
                        var achieved = Math.Round(Math.Clamp(record.Percentage, 0m, 100m), 4);
                        var nodes = record.NodesCount;
                        var maxPoints = totalIpnwNodes > 0m
                            ? Math.Round((nodes / totalIpnwNodes) * (decimal)kpi.PointsApplicable, 4)
                            : (ipnwResults.Count > 0 ? Math.Round((decimal)kpi.PointsApplicable / ipnwResults.Count, 4) : 0m);

                        Console.WriteLine($"INSERTING {kpi.KeyPerformanceIndicators} Area={area} Achieved={achieved} Nodes={nodes} MaxPoints={maxPoints}");
                        results.Add(new OverallKpiResult
                        {
                            KpiCode = $"KPI-{kpi.Id}",
                            KpiDefinitionId = kpi.Id,
                            KpiName = kpi.KeyPerformanceIndicators,
                            Platform = kpi.Perspectives,
                            AreaCode = area,
                            TargetValue = 100m,
                            AchievedKpi = achieved,
                            MaximumPointsPerKpi = maxPoints,
                            PointsAchieved = CalculatePointsAchieved(maxPoints, achieved, 100m),
                            Month = month,
                            Year = year,
                            CalculatedAt = nowUtc
                        });
                    }
                    continue;
                }

                if (kpi.KeyPerformanceIndicators.Equals("Routine Maintenance - SLBN/SDH", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("ENTERED SLBN BLOCK");
                    var totalSlbnNodes = slbnResults.Sum(x => x.NodesCount);
                    foreach (var record in slbnResults.Where(x => x.NodesCount > 0m))
                    {
                        var area = record.NormalizedAreaCode;
                        var achieved = record.Percentage;
                        var nodes = record.NodesCount;
                        var maxPoints = totalSlbnNodes > 0m
                            ? Math.Round((nodes / totalSlbnNodes) * (decimal)kpi.PointsApplicable, 4)
                            : (slbnResults.Count > 0 ? Math.Round((decimal)kpi.PointsApplicable / slbnResults.Count, 4) : 0m);

                        Console.WriteLine($"INSERTING {kpi.KeyPerformanceIndicators} Area={area} Achieved={achieved} Nodes={nodes} MaxPoints={maxPoints}");
                        results.Add(new OverallKpiResult
                        {
                            KpiCode = $"KPI-{kpi.Id}",
                            KpiDefinitionId = kpi.Id,
                            KpiName = kpi.KeyPerformanceIndicators,
                            Platform = kpi.Perspectives,
                            AreaCode = area,
                            TargetValue = 100m,
                            AchievedKpi = achieved,
                            MaximumPointsPerKpi = maxPoints,
                            PointsAchieved = CalculatePointsAchieved(maxPoints, achieved, 100m),
                            Month = month,
                            Year = year,
                            CalculatedAt = nowUtc
                        });
                    }
                    continue;
                }

                if (kpi.KeyPerformanceIndicators.Equals("Routine Maintenance - MSAN/OLTE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("ENTERED MSAN BLOCK");
                    var totalMsanNodes = msanResults.Sum(x => x.NodesCount);
                    foreach (var record in msanResults.Where(x => x.NodesCount > 0m))
                    {
                        var area = record.NormalizedAreaCode;
                        var achieved = record.Percentage;
                        var nodes = record.NodesCount;
                        var maxPoints = totalMsanNodes > 0m
                            ? Math.Round((nodes / totalMsanNodes) * (decimal)kpi.PointsApplicable, 4)
                            : (msanResults.Count > 0 ? Math.Round((decimal)kpi.PointsApplicable / msanResults.Count, 4) : 0m);

                        Console.WriteLine($"INSERTING {kpi.KeyPerformanceIndicators} Area={area} Achieved={achieved} Nodes={nodes} MaxPoints={maxPoints}");
                        results.Add(new OverallKpiResult
                        {
                            KpiCode = $"KPI-{kpi.Id}",
                            KpiDefinitionId = kpi.Id,
                            KpiName = kpi.KeyPerformanceIndicators,
                            Platform = kpi.Perspectives,
                            AreaCode = area,
                            TargetValue = 100m,
                            AchievedKpi = achieved,
                            MaximumPointsPerKpi = maxPoints,
                            PointsAchieved = CalculatePointsAchieved(maxPoints, achieved, 100m),
                            Month = month,
                            Year = year,
                            CalculatedAt = nowUtc
                        });
                    }
                    continue;
                }

                if (IsTowerMaintenanceKpi(kpi))
                {
                    Console.WriteLine("ENTERED TOWER BLOCK");
                    var totalTowerNodes = towerResults.Sum(x => x.NodesCount);
                    foreach (var record in towerResults.Where(x => x.NodesCount > 0m))
                    {
                        var area = record.NormalizedAreaCode;
                        var achieved = record.Percentage;
                        var nodes = record.NodesCount;

                        var maxPoints = totalTowerNodes > 0m
                            ? Math.Round((nodes / totalTowerNodes) * (decimal)kpi.PointsApplicable, 4)
                            : (towerResults.Count > 0 ? Math.Round((decimal)kpi.PointsApplicable / towerResults.Count, 4) : 0m);

                        Console.WriteLine($"INSERTING {kpi.KeyPerformanceIndicators} Area={area} Achieved={achieved} Nodes={nodes} MaxPoints={maxPoints}");
                        results.Add(new OverallKpiResult
                        {
                            KpiCode = $"KPI-{kpi.Id}",
                            KpiDefinitionId = kpi.Id,
                            KpiName = kpi.KeyPerformanceIndicators,
                            Platform = kpi.Perspectives,
                            AreaCode = area,
                            TargetValue = 95m,
                            AchievedKpi = achieved,
                            MaximumPointsPerKpi = maxPoints,
                            PointsAchieved = CalculatePointsAchieved(maxPoints, achieved, 95m),
                            Month = month,
                            Year = year,
                            CalculatedAt = nowUtc
                        });
                    }
                    continue;
                }

                if (_useComponentPowerAcKpi && _powerAcKpiDefinitionId == kpi.Id)
                {
                    // Four-component Power & AC KPI, selected by KPI id (not by name).
                    // Uses the SAVED monthly snapshot (Power & A/C KPI -> Calculate & Save), so the overall
                    // scorecard shows exactly the reviewed result. Only Complete groups are written; Incomplete or
                    // Pending groups and months without a saved snapshot get no row (never a silent zero).
                    var savedPowerAc = await _db.PowerAcKpiResults.AsNoTracking()
                        .Include(r => r.EngineerGroup)
                        .Where(r => r.Year == year
                            && r.PeriodType == backend.Models.PowerAc.PowerAcPeriodTypes.Month
                            && r.PeriodNumber == month)
                        .ToListAsync();
                    if (savedPowerAc.Count == 0)
                        Console.WriteLine($"Power & AC: no saved snapshot for {year}-{month:00}; KPI {kpi.Id} left without results.");

                    foreach (var saved in savedPowerAc.Where(r =>
                        r.Status == backend.Models.PowerAc.PowerAcResultStatus.Complete
                        && r.CompositeAchievement.HasValue && r.MaximumPoints.HasValue && r.PointsAchieved.HasValue))
                    {
                        var region = dbRegions.FirstOrDefault(r => r.Id == saved.EngineerGroup!.RegionDataId);
                        var area = CanonicalizeArea(region?.LeaCode, officialAreas);
                        if (area == string.Empty) continue;

                        results.Add(new OverallKpiResult
                        {
                            KpiCode = $"KPI-{kpi.Id}",
                            KpiDefinitionId = kpi.Id,
                            KpiName = kpi.KeyPerformanceIndicators,
                            Platform = kpi.Perspectives,
                            AreaCode = area,
                            TargetValue = Math.Round(saved.CompositeTarget * 100m, 4),
                            AchievedKpi = Math.Round(saved.CompositeAchievement!.Value * 100m, 4),
                            MaximumPointsPerKpi = saved.MaximumPoints!.Value,
                            PointsAchieved = saved.PointsAchieved!.Value,
                            Month = month,
                            Year = year,
                            CalculatedAt = nowUtc
                        });
                    }
                    continue;
                }

                if (IsPowerAndAcKpi(kpi.KeyPerformanceIndicators))
                {
                    Console.WriteLine("ENTERED POWER & AC BLOCK");
                    var totalPowerNodes = powerResults.Sum(x => x.NodesCount);
                    foreach (var record in powerResults.Where(x => x.NodesCount > 0m))
                    {
                        var area = record.NormalizedAreaCode;
                        var achieved = record.Percentage;
                        var nodes = record.NodesCount;

                        var maxPoints = totalPowerNodes > 0m
                            ? Math.Round((nodes / totalPowerNodes) * (decimal)kpi.PointsApplicable, 4)
                            : (powerResults.Count > 0 ? Math.Round((decimal)kpi.PointsApplicable / powerResults.Count, 4) : 0m);

                        Console.WriteLine($"INSERTING {kpi.KeyPerformanceIndicators} Area={area} Achieved={achieved} Nodes={nodes} MaxPoints={maxPoints}");
                        results.Add(new OverallKpiResult
                        {
                            KpiCode = $"KPI-{kpi.Id}",
                            KpiDefinitionId = kpi.Id,
                            KpiName = kpi.KeyPerformanceIndicators,
                            Platform = kpi.Perspectives,
                            AreaCode = area,
                            TargetValue = 90m,
                            AchievedKpi = achieved,
                            MaximumPointsPerKpi = maxPoints,
                            PointsAchieved = CalculatePointsAchieved(maxPoints, achieved, 90m),
                            Month = month,
                            Year = year,
                            CalculatedAt = nowUtc
                        });
                    }
                    continue;
                }

                if (IsTelemetryKpi(kpi.KeyPerformanceIndicators))
                {
                    Console.WriteLine("ENTERED TELEMETRY BLOCK");
                    if (telemetryMetrics.Count == 0)
                    {
                        continue;
                    }
                    var totalTelemetryNodes = (decimal)telemetryMetrics.Sum(x => x.Node_Count ?? 0);
                    foreach (var record in telemetryMetrics)
                    {
                        var designation = record.Designation?.Trim() ?? string.Empty;
                        var areaCode = ResolveMappedAreaCode(designationToArea, designation, officialAreas);
                        areaCode ??= string.Empty;
                        if (string.IsNullOrEmpty(areaCode)) continue;

                        var achieved = record.Percentage;
                        var nodes = (decimal)(record.Node_Count ?? 0);

                        var maxPoints = totalTelemetryNodes > 0m
                            ? Math.Round((nodes / totalTelemetryNodes) * (decimal)kpi.PointsApplicable, 4)
                            : (telemetryMetrics.Count > 0 ? Math.Round((decimal)kpi.PointsApplicable / telemetryMetrics.Count, 4) : 0m);

                        var targetValue = TryParseTargetValue(kpi.DescriptionOfKPI) ?? 99.990m;

                        Console.WriteLine($"INSERTING {kpi.KeyPerformanceIndicators} Area={areaCode} Achieved={achieved} Nodes={nodes} MaxPoints={maxPoints}");
                        results.Add(new OverallKpiResult
                        {
                            KpiCode = $"KPI-{kpi.Id}",
                            KpiDefinitionId = kpi.Id,
                            KpiName = kpi.KeyPerformanceIndicators,
                            Platform = kpi.Perspectives,
                            AreaCode = areaCode,
                            TargetValue = targetValue,
                            AchievedKpi = achieved,
                            MaximumPointsPerKpi = maxPoints,
                            PointsAchieved = CalculatePointsAchieved(maxPoints, achieved, targetValue),
                            Month = month,
                            Year = year,
                            CalculatedAt = nowUtc
                        });
                    }
                    continue;
                }

                var candidates = sourceFilter.Any()
                    ? allNamedKpis.Where(x => sourceFilter.Contains(x.Source)).ToList()
                    : allNamedKpis;

                var matchedKpi = FindBestMatch(kpi.KeyPerformanceIndicators, candidates);
                if (matchedKpi == null)
                {
                    continue;
                }

                var snapshots = BuildAreaSnapshots(matchedKpi, ipMetrics, bbMetrics, otn1Metrics, otn2Metrics, sfMetrics, entMetrics, otherMetrics, enterpriseTargets, daysInMonth, officialAreas);

                var areaSnapshots = normalizedAreas
                    .Select(area => (area, snapshot: FindSnapshotForArea(snapshots, NormalizeArea(area))))
                    .ToList();

                var snapshotNodeValues = snapshots.Values.ToList();
                bool hasNodeBasedWeight = snapshotNodeValues.Any(x => x.TotalNodes > 0m);
                decimal totalNodes = hasNodeBasedWeight
                    ? snapshotNodeValues.Sum(x => x.TotalNodes)
                    : 0m;
                decimal equalShare = normalizedAreas.Count > 0
                    ? (decimal)kpi.PointsApplicable / normalizedAreas.Count
                    : 0m;

                var isFibreRestoration = IsFibreFailuresRestorationKpi(kpi.KeyPerformanceIndicators);
                var numEngineers = dbRegions.Any()
                    ? dbRegions.Select(x => x.NetworkEngineer?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    : (normalizedAreas.Count > 0 ? normalizedAreas.Count : 1);

                if (numEngineers <= 0) numEngineers = 1;
                var pointsPerEngineer = (decimal)kpi.PointsApplicable / numEngineers;

                foreach (var (area, snapshot) in areaSnapshots)
                {
                    // Do not persist a synthetic KPI result for an availability row with no denominator.
                    // The frontend renders the missing result as "-".
                    if (snapshot?.IsUnavailable == true)
                    {
                        continue;
                    }

                    var achieved = Math.Round(Math.Clamp(snapshot?.Achieved ?? 0m, 0m, 100m), 4);
                    var maxPoints = isFibreRestoration
                        ? Math.Round(pointsPerEngineer, 4)
                        : (hasNodeBasedWeight && totalNodes > 0m
                            ? Math.Round(((decimal)kpi.PointsApplicable * (snapshot?.TotalNodes ?? 0m)) / totalNodes, 4)
                            : Math.Round(equalShare, 4));

                    // Calculate pointsAchieved based on target value
                    var targetValue = TryParseTargetValue(kpi.DescriptionOfKPI);
                    var pointsAchieved = snapshot?.NormalizedAchieved is decimal normalizedAchieved
                        ? Math.Round(maxPoints * normalizedAchieved, 4)
                        : CalculatePointsAchieved(maxPoints, achieved, targetValue);

                    results.Add(new OverallKpiResult
                    {
                        KpiCode = $"KPI-{kpi.Id}",
                        KpiDefinitionId = kpi.Id,
                        KpiName = kpi.KeyPerformanceIndicators,
                        Platform = kpi.Perspectives,
                        AreaCode = area,
                        TargetValue = targetValue ?? 0m,
                        AchievedKpi = achieved,
                        MaximumPointsPerKpi = maxPoints,
                        PointsAchieved = pointsAchieved,
                        Month = month,
                        Year = year,
                        CalculatedAt = nowUtc
                    });
                }
            }

            var existing = await _db.OverallKpiResults
                .Where(x => x.Month == month && x.Year == year)
                .ToListAsync();

            if (existing.Any())
            {
                _db.OverallKpiResults.RemoveRange(existing);
            }

            var uniqueResults = results
                .Where(r => !string.IsNullOrWhiteSpace(r.AreaCode))
                .GroupBy(r => new { r.KpiDefinitionId, r.Year, r.Month, r.AreaCode, r.Platform })
                .Select(g =>
                {
                    var first = g.First();
                    if (g.Count() == 1) return first;

                    var totalMax = g.Sum(x => x.MaximumPointsPerKpi);
                    first.MaximumPointsPerKpi = totalMax;
                    first.PointsAchieved = g.Sum(x => x.PointsAchieved);
                    first.AchievedKpi = totalMax > 0m
                        ? Math.Round(g.Sum(x => x.AchievedKpi * x.MaximumPointsPerKpi) / totalMax, 4)
                        : Math.Round(g.Average(x => x.AchievedKpi), 4);
                    return first;
                })
                .ToList();

            var overallPercentByArea = uniqueResults
                .GroupBy(x => x.AreaCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var totalMax = g.Sum(x => x.MaximumPointsPerKpi);
                        var totalAchieved = g.Sum(x => x.PointsAchieved);
                        return totalMax > 0m
                            ? Math.Round((totalAchieved / totalMax) * 100m, 4)
                            : 0m;
                    },
                    StringComparer.OrdinalIgnoreCase);

            foreach (var row in uniqueResults)
            {
                if (overallPercentByArea.TryGetValue(row.AreaCode, out var percent))
                {
                    row.OverallKpiValuePercent = percent;
                }
            }

            _db.OverallKpiResults.AddRange(uniqueResults);
            await _db.SaveChangesAsync();

            return uniqueResults
                .OrderBy(x => x.KpiDefinitionId)
                .ThenBy(x => x.AreaCode)
                .ToList();
        }

        // =========================================================
        // AREA SNAPSHOT BUILDER
        // Constructs a dictionary of area codes to calculated achieved KPI values
        // Each platform uses different calculation methods:
        // - IP/BB: Availability = (TotalMinutes - UnavailableMinutes) / (24*60*days*nodes) * 100
        // - OTN Op1: Same availability calculation as IP/BB
        // - OTN Op2: SLA Ratio = LinksSlaNotViolated / TotalFailedLinks * 100
        // - Service Fulfilment: Direct KPI value (already calculated)
        // =========================================================
        private static Dictionary<string, AreaSnapshot> BuildAreaSnapshots(
            NamedKpi? matchedKpi,
            List<IpNwOpKpiMetric> ipMetrics,
            List<BbAnwKpiNode> bbMetrics,
            List<OtnOp1Metrics> otn1Metrics,
            List<OtnOp2Metrics> otn2Metrics,
            List<ServiceFulfilmentKpiMetric> sfMetrics,
            List<EnterpriseKpiMetric> entMetrics,
            List<OtherOperatorKpiMetric> otherMetrics,
            IReadOnlyDictionary<int, decimal> enterpriseTargets,
            int daysInMonth,
            IReadOnlyCollection<string> officialAreas)
        {
            var result = new Dictionary<string, AreaSnapshot>();
            if (matchedKpi == null) return result;

            if (matchedKpi.Source == "ip")
            {
                foreach (var row in ipMetrics.Where(x => x.IpNwOpKpiId == matchedKpi.Id))
                {
                    var area = CanonicalizeArea(row.AreaCode, officialAreas);
                    if (area == string.Empty) continue;
                    var nodeWeight = GetIpNodeWeight(row, daysInMonth);
                    var achieved = CalculateAvailability(row.TotalMinutes, row.UnavailableMinutes, nodeWeight, daysInMonth);
                    result[area] = achieved.HasValue
                        ? new AreaSnapshot(achieved.Value, nodeWeight)
                        : new AreaSnapshot(0m, nodeWeight, IsUnavailable: true);
                }
                return result;
            }

            if (matchedKpi.Source == "bb")
            {
                foreach (var row in bbMetrics.Where(x => x.BbAnwKpiId == matchedKpi.Id))
                {
                    var area = CanonicalizeArea(row.NodeCode, officialAreas);
                    if (area == string.Empty) continue;
                    var achieved = CalculateAvailability(row.TotalMinutes, row.UnavailableMinutes, row.TotalNodes, daysInMonth);
                    result[area] = achieved.HasValue
                        ? new AreaSnapshot(achieved.Value, row.TotalNodes ?? 0)
                        : new AreaSnapshot(0m, row.TotalNodes ?? 0, IsUnavailable: true);
                }
                return result;
            }

            if (matchedKpi.Source == "otn1")
            {
                foreach (var row in otn1Metrics.Where(x => x.OtnOp1Id == matchedKpi.Id))
                {
                    var area = CanonicalizeArea(row.Site, officialAreas);
                    if (area == string.Empty) continue;
                    var achieved = CalculateAvailability(row.TotalMinutes, row.UnavailableMinutes, row.TotalNodes, daysInMonth);
                    result[area] = achieved.HasValue
                        ? new AreaSnapshot(achieved.Value, row.TotalNodes)
                        : new AreaSnapshot(0m, row.TotalNodes, IsUnavailable: true);
                }
                return result;
            }

            if (matchedKpi.Source == "otn2")
            {
                foreach (var row in otn2Metrics.Where(x => x.OtnOp2Id == matchedKpi.Id))
                {
                    var area = CanonicalizeArea(row.Site, officialAreas);
                    if (area == string.Empty) continue;
                    var achieved = CalculateSlaRatio(row.TotalFailedLinks, row.LinksSlaNotViolated);
                    result[area] = new AreaSnapshot(achieved, row.TotalFailedLinks);
                }
                return result;
            }

            if (matchedKpi.Source == "ent")
            {
                foreach (var row in entMetrics.Where(x => x.EnterpriseKpiId == matchedKpi.Id))
                {
                    var area = CanonicalizeArea(row.AreaCode, officialAreas);
                    if (area == string.Empty) continue;

                    result[area] = row.KpiValue.HasValue
                        ? new AreaSnapshot(Math.Round(Math.Clamp(row.KpiValue.Value, 0m, 100m), 4), 0m)
                        : new AreaSnapshot(0m, 0m, IsUnavailable: true);
                }

                return result;
            }


           /* if (matchedKpi.Source == "ent")
            {
                bool isFaultRate =
                    matchedKpi.Name.Contains("Fault Rate",
                        StringComparison.OrdinalIgnoreCase);

                foreach (var row in entMetrics.Where(x => x.EnterpriseKpiId == matchedKpi.Id))
                {
                    var area = CanonicalizeArea(row.AreaCode, officialAreas);
                    if (area == string.Empty) continue;

                    if (!row.KpiValue.HasValue)
                    {
                        result[area] = new AreaSnapshot(0m, 0m, IsUnavailable: true);
                        continue;
                    }

                    decimal achieved = isFaultRate
                        ? 100m - row.KpiValue.Value
                        : row.KpiValue.Value;

                    result[area] = new AreaSnapshot(
                        Math.Round(Math.Clamp(achieved, 0m, 100m), 4),
                        0m);
                }

                return result;
            }*/


            if (matchedKpi.Source == "other")
            {
                foreach (var row in otherMetrics.Where(x => x.OtherOperatorKpiId == matchedKpi.Id))
                {
                    var area = CanonicalizeArea(row.Site, officialAreas);
                    if (area == string.Empty) continue;

                    result[area] = row.KpiValue.HasValue
                        ? new AreaSnapshot(Math.Round(Math.Clamp(row.KpiValue.Value, 0m, 100m), 4), 0m)
                        : new AreaSnapshot(0m, 0m, IsUnavailable: true);
                }

                return result;
            }

            /*
            if (matchedKpi.Source == "other")
            {
                bool isFaultRate =
                    matchedKpi.Name.Contains("Fault Rate",
                        StringComparison.OrdinalIgnoreCase);

                foreach (var row in entMetrics.Where(x => x.EnterpriseKpiId == matchedKpi.Id))
                {
                    var area = CanonicalizeArea(row.AreaCode, officialAreas);
                    if (area == string.Empty) continue;

                    if (!row.KpiValue.HasValue)
                    {
                        result[area] = new AreaSnapshot(0m, 0m, IsUnavailable: true);
                        continue;
                    }

                    decimal achieved = isFaultRate
                        ? 100m - row.KpiValue.Value
                        : row.KpiValue.Value;

                    result[area] = new AreaSnapshot(
                        Math.Round(Math.Clamp(achieved, 0m, 100m), 4),
                        0m);
                }

                return result;
            }
            */
            foreach (var row in sfMetrics.Where(x => x.ServiceFulfilmentKpiId == matchedKpi.Id))
            {
                var area = CanonicalizeArea(row.AreaCode, officialAreas);
                if (area == string.Empty) continue;
                /*result[area] = row.KpiValue.HasValue
                    ? new AreaSnapshot(Math.Clamp(row.KpiValue.Value, 0m, 90m), 0m)
                    : new AreaSnapshot(0m, 0m, IsUnavailable: true);*/


                result[area] = row.KpiValue.HasValue
    ? new AreaSnapshot(
        row.KpiValue.Value >= 90m
            ? 100m
            : Math.Clamp(row.KpiValue.Value, 0m, 100m),
        0m)
    : new AreaSnapshot(0m, 0m, IsUnavailable: true);
            }
            return result;
        }

        // =========================================================
        // KPI MATCHING ALGORITHM
        // Finds best-matching platform KPI given a master KPI name
        // Uses fuzzy text matching with normalized scores:
        // - 100: Exact match (ignoring spaces/special chars)
        // - 85: Substring containment
        // - 0-84: Token overlap ratio (Jaccard similarity)
        // Minimum score threshold: 35 points
        // =========================================================
        private static NamedKpi? FindBestMatch(string kpiName, List<NamedKpi> sourceKpis)
        {
            var normalizedTarget = NormalizeText(kpiName);
            if (normalizedTarget == string.Empty) return null;

            NamedKpi? best = null;
            decimal bestScore = 0m;

            foreach (var candidate in sourceKpis)
            {
                var score = Score(normalizedTarget, NormalizeText(candidate.Name));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return bestScore >= 35m ? best : null;
        }

        // =========================================================
        // TEXT MATCHING SCORE
        // Calculates similarity score between target and candidate text
        // Scoring rules:
        // - 100: Exact match (ignoring spaces/special chars)
        // - 85: Substring match on both directions
        // - 0-84: Jaccard token overlap ratio * 100
        // Returns score as decimal (0-100)
        // =========================================================
        private static decimal Score(string target, string candidate)
        {
            if (target == string.Empty || candidate == string.Empty) return 0m;
            var targetCompact = target.Replace(" ", string.Empty);
            var candidateCompact = candidate.Replace(" ", string.Empty);

            if (targetCompact == candidateCompact) return 100m;
            if (targetCompact.Contains(candidateCompact) || candidateCompact.Contains(targetCompact)) return 85m;

            var targetTokens = Tokenize(target);
            var candidateTokens = Tokenize(candidate);
            if (!targetTokens.Any() || !candidateTokens.Any()) return 0m;

            var overlap = targetTokens.Intersect(candidateTokens).Count();
            if (overlap == 0) return 0m;

            var ratio = (decimal)(2 * overlap) / (targetTokens.Count + candidateTokens.Count);
            return ratio * 80m;
        }

        // =========================================================
        // FIND SNAPSHOT FOR AREA
        // Locates the AreaSnapshot for a given area code
        // First tries exact match, then partial match with longest common prefix
        // Handles area code variations and mappings
        // =========================================================
        private static AreaSnapshot? FindSnapshotForArea(Dictionary<string, AreaSnapshot> snapshots, string normalizedArea)
        {
            if (normalizedArea == string.Empty || snapshots.Count == 0) return null;
            if (snapshots.TryGetValue(normalizedArea, out var exact)) return exact;

            // Handle special mappings for combined metro areas (CEN/HK/MD)
            if (normalizedArea == "hk" || normalizedArea == "cenmd")
            {
                if (snapshots.TryGetValue("cenhkmd", out var s1)) return s1;
                if (snapshots.TryGetValue("cenhkmd1", out var s2)) return s2;
                if (snapshots.TryGetValue("cenhk", out var s3)) return s3;
            }

            return null;
        }

        // =========================================================
        // COMMON PREFIX LENGTH
        // Calculates length of matching prefix between two strings
        // Used for partial area code matching to find best available data
        // =========================================================
        private static int CommonPrefixLength(string a, string b)
        {
            var len = Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < len && a[i] == b[i]) i++;
            return i;
        }

        // =========================================================
        // AVAILABILITY CALCULATIONS (OVERLOADED)
        // Computes network availability percentage from minutes data
        // Formula: ((TotalMinutes - UnavailableMinutes) / (24*60*days*nodes)) * 100
        // Multiple overloads support different input types (long?, int?, decimal)
        // Clamped to 0-100 range
        // Returns null if denominator is 0 (no data available)
        // =========================================================
        private static decimal? CalculateAvailability(long? totalMinutes, int? unavailableMinutes, int? totalNodes, int daysInMonth)
        {
            decimal tm = totalMinutes ?? 0;
            decimal um = unavailableMinutes ?? 0;
            decimal tn = totalNodes ?? 0;

            var denominator = tm > 0m
                ? tm
                : (24m * 60m * daysInMonth * tn);
            if (denominator <= 0m) return null;

            var numerator = denominator - um;
            var pct = (numerator / denominator) * 100m;
            return Math.Clamp(pct, 0m, 100m);
        }

        private static decimal? CalculateAvailability(long? totalMinutes, int? unavailableMinutes, decimal totalNodes, int daysInMonth)
        {
            decimal tm = totalMinutes ?? 0;
            decimal um = unavailableMinutes ?? 0;
            decimal tn = totalNodes;

            var denominator = tm > 0m
                ? tm
                : (24m * 60m * daysInMonth * tn);
            if (denominator <= 0m) return null;

            var numerator = denominator - um;
            var pct = (numerator / denominator) * 100m;
            return Math.Clamp(pct, 0m, 100m);
        }

        private static decimal? CalculateAvailability(int totalMinutes, int unavailableMinutes, int totalNodes, int daysInMonth)
            => CalculateAvailability((long)totalMinutes, unavailableMinutes, (int?)totalNodes, daysInMonth);
        // =========================================================
        // SLA RATIO CALCULATION
        // Calculates Service Level Agreement compliance ratio
        // Used for OTN Op2 (failed link SLA tracking)
        // Formula: (LinksSlaNotViolated / TotalFailedLinks) * 100
        // Returns 100% if no failed links reported
        // =========================================================
        private static decimal CalculateSlaRatio(int totalFailedLinks, int linksSlaNotViolated)
        {
            if (totalFailedLinks <= 0) return 100m;
            var pct = ((decimal)linksSlaNotViolated / totalFailedLinks) * 100m;
            return Math.Clamp(pct, 0m, 100m);
        }

        private static decimal CalculateOtherMetricPercentage(OtherKpiMetric row)
        {
            return Math.Clamp(row.KpiValue ?? 0m, 0m, 100m);
        }

        private static bool IsTelemetryKpi(string? kpiName)
            => (kpiName ?? string.Empty).Contains("Telemetry", StringComparison.OrdinalIgnoreCase);

        private static bool IsPowerAndAcKpi(string? kpiName)
        {
            var normalized = NormalizeArea(kpiName);
            return normalized.Contains("power")
                && (normalized.Contains("air")
                    || normalized.Contains("ac")
                    || normalized.Contains("oc")
                    || normalized.Contains("airconditioning"));
        }

        private static bool IsTowerMaintenanceKpi(KpiDefinition kpi)
        {
            var normalizedName = NormalizeText(kpi.KeyPerformanceIndicators);
            var normalizedCategory = NormalizeText(kpi.Category);
            return (normalizedName.Contains("tower") || normalizedCategory.Contains("tower"))
                && normalizedName.Contains("maintenance");
        }

        // =========================================================
        // TEXT NORMALIZATION HELPERS
        // NormalizeText: For KPI name matching (preserves spaces for tokenization)
        // NormalizeArea: For area code matching (compact representation, no spaces)
        // Removes special characters, converts to lowercase for comparison
        // =========================================================
        private static string NormalizeText(string? value)
            => Regex.Replace(value ?? string.Empty, "[^A-Za-z0-9]+", " ").Trim().ToLowerInvariant();

        private static string NormalizeArea(string? value)
        {
            var normalized = Regex.Replace(value ?? string.Empty, "[^A-Za-z0-9]+", "").ToLowerInvariant();
            if (normalized == "kpivalue") return string.Empty;

            // Map metric area code variations/shorthands to standard normalized LEA codes
            // return normalized switch
            // {
            //     "ndfrm" => "ndrm",
            //     "ngivt" => "ngwt",
            //     "debkymt" => "kymt",
            //     "bddwmrg" => "bdbwmrg",
            //     "keirn" => "kern",
            //     "embmbmh" => "embhbmh",
            //     "bcjrdkltc" => "bcapkltc",
            //     "adipr" => "adpr",
            //     "konix" => "konkx",
            //     _ => normalized
            // };
            // REMOVE the switch statement that translates ndfrm -> ndrm, etc.
            // Just return the normalized string directly.
            return normalized;
        }

        private static string CanonicalizeArea(string? value, IReadOnlyCollection<string>? officialAreas)
        {
            var normalized = NormalizeArea(value);
            if (normalized == string.Empty) return string.Empty;
            if (officialAreas == null || officialAreas.Count == 0) return normalized;

            var exact = officialAreas.FirstOrDefault(area => area.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exact)) return exact;

            var trailingSegment = Regex.Split(value ?? string.Empty, @"[/\\\-\s]+")
                .Select(NormalizeArea)
                .Where(x => x != string.Empty)
                .OrderByDescending(x => x.Length)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(trailingSegment))
            {
                exact = officialAreas.FirstOrDefault(area => area.Equals(trailingSegment, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(exact)) return exact;
            }

            return normalized;
        }

        // =========================================================
        // TOKENIZATION FOR MATCHING
        // Splits normalized text into meaningful word tokens
        // Handles camelCase splitting (KPI -> k pi)
        // Filters out single-character tokens
        // Used for fuzzy KPI matching via token overlap
        // =========================================================
        private static List<string> Tokenize(string normalized)
        {
            var raw = Regex.Replace(normalized ?? string.Empty, "([a-z])([A-Z])", "$1 $2");
            return Regex.Split(raw, @"[^A-Za-z0-9]+")
                .Where(x => x.Trim().Length >= 2 || (x.Trim().Length == 1 && char.IsDigit(x.Trim()[0])))
                .Select(x => x.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();
        }

        private static string NormalizeDesignation(string value)
        {
            return new string(
                value
                    .ToUpperInvariant()
                    .Where(char.IsLetterOrDigit)
                    .ToArray());
        }

        // Returns normalized designation aliases so source rows with an optional NW/ prefix
        // can match the corresponding engineer code stored in RegionData.
        private static HashSet<string> GetDesignationAliases(string? value)
        {
            var normalized = NormalizeDesignation(StripDesignationSuffix(value ?? string.Empty));
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (normalized == string.Empty) return aliases;

            aliases.Add(normalized);
            if (normalized.StartsWith("NW", StringComparison.OrdinalIgnoreCase) && normalized.Length > 2)
            {
                aliases.Add(normalized[2..]);
            }

            return aliases;
        }

        private static string StripDesignationSuffix(string value)
        {
            var designation = value?.Trim() ?? string.Empty;
            if (designation == string.Empty) return string.Empty;

            var idx = designation.IndexOf('(');
            return idx >= 0
                ? designation[..idx].Trim()
                : designation;
        }

        private static string ResolveAreaCodeFromDesignation(string designation, List<RegionData> dbRegions, IReadOnlyCollection<string> officialAreas)
        {
            var baseDesignation = StripDesignationSuffix(designation);
            if (baseDesignation == string.Empty) return string.Empty;

            var designationAliases = GetDesignationAliases(baseDesignation);
            var region = dbRegions.FirstOrDefault(r =>
            {
                var engineerDesignation = StripDesignationSuffix(r.NetworkEngineer ?? string.Empty);
                return GetDesignationAliases(engineerDesignation)
                    .Any(designationAliases.Contains);
            });

            if (region != null)
            {
                var regionCode = string.IsNullOrWhiteSpace(region.LeaCode)
                    ? region.NetworkEngineer
                    : region.LeaCode;
                return CanonicalizeArea(regionCode, officialAreas);
            }

            return CanonicalizeArea(baseDesignation, officialAreas);
        }

        private static string ResolveMappedAreaCode(
            IReadOnlyDictionary<string, string> designationToArea,
            string designation,
            IReadOnlyCollection<string> officialAreas)
        {
            if (designationToArea.TryGetValue(designation, out var direct) && !string.IsNullOrWhiteSpace(direct))
            {
                return CanonicalizeArea(direct, officialAreas);
            }

            var baseDesignation = StripDesignationSuffix(designation);
            if (baseDesignation != designation
                && designationToArea.TryGetValue(baseDesignation, out var fromBase)
                && !string.IsNullOrWhiteSpace(fromBase))
            {
                return CanonicalizeArea(fromBase, officialAreas);
            }

            foreach (var alias in GetDesignationAliases(baseDesignation))
            {
                if (designationToArea.TryGetValue(alias, out var fromAlias)
                    && !string.IsNullOrWhiteSpace(fromAlias))
                {
                    return CanonicalizeArea(fromAlias, officialAreas);
                }
            }

            return CanonicalizeArea(baseDesignation, officialAreas);
        }

        private Dictionary<string, string> BuildDesignationToAreaMap(List<RegionData> dbRegions, IReadOnlyCollection<string> officialAreas)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Collect designations from maintenance and telemetry tables
            var designations = _db.MsanMtcData.Select(x => x.Designation)
                .Concat(_db.IpnwMtcData.Select(x => x.Designation))
                .Concat(_db.SlbnMtcData.Select(x => x.Designation))
                .Concat(_db.TowerMtcData.Select(x => x.Designation))
                .Concat(_db.Telemetry.Select(x => x.Designation))
                .Concat(_db.PowerAndAC.Select(x => x.Designation))
                .Where(x => x != null)
                .Distinct()
                .ToList();

            foreach (var d in designations)
            {
                var designation = d?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(designation))
                    continue;

                var baseDesignation = StripDesignationSuffix(designation);
                var resolvedArea = ResolveAreaCodeFromDesignation(designation, dbRegions, officialAreas);
                if (resolvedArea == string.Empty)
                {
                    Console.WriteLine($"No RegionData/LEA match for designation [{designation}] (base: [{baseDesignation}])");
                    continue;
                }

                map[designation] = resolvedArea;
                map[baseDesignation] = resolvedArea;
                map[NormalizeArea(baseDesignation)] = resolvedArea;

                foreach (var alias in GetDesignationAliases(baseDesignation))
                {
                    map[alias] = resolvedArea;
                }
            }

            return map;
        }

        // =========================================================
        // ENTITY TO DTO MAPPING
        // Converts OverallKpiResult entity to DTO for API response
        // Excludes internal fields like CalculatedAt
        // =========================================================
        private static OverallKpiResultDto ToDto(OverallKpiResult x) => new()
        {
            Id = x.Id,
            KpiDefinitionId = x.KpiDefinitionId,
            KpiName = x.KpiName,
            AreaCode = x.AreaCode,
            AchievedKpi = x.AchievedKpi,
            MaximumPointsPerKpi = x.MaximumPointsPerKpi,
            PointsAchieved = x.PointsAchieved,
            OverallKpiValuePercent = x.OverallKpiValuePercent,
            Month = x.Month,
            Year = x.Year
        };

        // =========================================================
        // HELPER RECORD TYPES
        // NamedKpi: Platform-specific KPI definition (source, id, name)
        // AreaSnapshot: Calculated KPI value and node weight for an area
        // =========================================================
        private sealed record NamedKpi(string Source, int Id, string Name);
        private sealed record AreaSnapshot(
            decimal Achieved,
            decimal TotalNodes,
            decimal? NormalizedAchieved = null,
            bool IsUnavailable = false);

        // =========================================================
        // POINT CALCULATION WITH TARGET-BASED SCALING
        // Calculates points achieved based on target value:
        // - No target: Linear scaling (points = maxPoints * achieved / 100)
        // - With target:
        //   * If achieved > target: Full maxPoints awarded
        //   * Otherwise: Scaled by ratio (points = maxPoints * achieved / target)
        // This provides incentive to exceed targets
        // =========================================================
        private static decimal CalculatePointsAchieved(decimal maxPoints, decimal achieved, decimal? targetValue)
        {
            // If no target value or target is 0 or negative, use simple linear scaling: points = maxPoints * achieved / 100
            if (!targetValue.HasValue || targetValue.Value <= 0)
            {
                return Math.Round((maxPoints * achieved) / 100m, 4);
            }

            // Target-based formula:
            // If achieved >= target: pointsAchieved = maxPoints
            // Else: pointsAchieved = maxPoints * achieved / 100
            var target = targetValue.Value;
            var points = achieved >= target
                ? maxPoints
                : Math.Round((maxPoints * achieved) / 100m, 4);

            return points;
        }

        // =========================================================
        // TARGET VALUE EXTRACTION
        // Extracts numeric target value from KPI description text
        // Looks for first decimal number using regex pattern \d+(\.\d+)?
        // Returns null if no numeric value found
        // Example: "99.5% availability target" -> 99.5
        // =========================================================
        private static decimal? TryParseTargetValue(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = Regex.Match(text, @"\d+(\.\d+)?");
            if (!m.Success) return null;
            return decimal.TryParse(m.Value, out var value) ? value : null;
        }

        private static decimal CalculateEnterpriseOrOtherNormalized(string kpiName, decimal actual, decimal target)
        {
            var normalizedName = NormalizeText(kpiName);

            if (normalizedName.Contains("fault clearance rate") || normalizedName.Contains("clearance rate"))
            {
                return actual > 0.9m ? 1m : actual / 0.9m;
            }

            if (target <= 0m)
            {
                return 0m;
            }

            return actual < target
                ? 1m
                : 1m - ((actual - target) / target);
        }

        // =========================================================
        // IP NODE WEIGHT CALCULATION
        // Determines node count for IP metrics when not provided
        // If TotalNodes is available, use directly
        // Otherwise, derive from TotalMinutes / minutes per node per month
        // Supports metric records with missing node counts
        // =========================================================
        private static decimal GetIpNodeWeight(IpNwOpKpiMetric row, int daysInMonth)
        {
            var nodes = (decimal)(row.TotalNodes ?? 0);
            if (nodes > 0m) return nodes;

            if (daysInMonth <= 0) return 0m;
            var totalMinutes = (decimal)(row.TotalMinutes ?? 0);
            if (totalMinutes <= 0m) return 0m;

            var minutesPerNode = 24m * 60m * daysInMonth;
            return totalMinutes / minutesPerNode;
        }

        private static bool IsFibreFailuresRestorationKpi(string kpiName)
        {
            if (string.IsNullOrEmpty(kpiName)) return false;
            var normalized = kpiName.Replace(" ", "").ToLowerInvariant();
            return normalized.Contains("fiberfailuresrestoration(general)")
                || normalized.Contains("fibrefailuresrestoration(general)")
                || normalized.Contains("fiberfailurerestoration(largescale")
                || normalized.Contains("fibrefailurerestoration(largescale");
        }

    }
}
