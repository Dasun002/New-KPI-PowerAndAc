using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace backend.Services
{
    public class IpnwMtcDataCumulativeService : IIpnwMtcDataCumulativeService
    {
        private static readonly Dictionary<string, int> MonthSequence =
            CultureInfo.InvariantCulture.DateTimeFormat.MonthNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select((name, index) => new { Name = name, Number = index + 1 })
                .ToDictionary(x => x.Name, x => x.Number, StringComparer.OrdinalIgnoreCase);

        private readonly AppDbContext _db;

        public IpnwMtcDataCumulativeService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IpnwMtcDataBackfillResult> RecalculateAllAsync()
        {
            var records = await _db.IpnwMtcData.ToListAsync();
            var groupsProcessed = 0;
            var recordsUpdated = 0;

            var groups = records
                .Where(x => !string.IsNullOrWhiteSpace(x.Designation) && !string.IsNullOrWhiteSpace(x.Year))
                .GroupBy(x => new
                {
                    Designation = x.Designation!.Trim(),
                    Year = x.Year!.Trim()
                });

            foreach (var group in groups)
            {
                groupsProcessed++;
                recordsUpdated += RecalculateGroup(group);
            }

            if (recordsUpdated > 0)
            {
                await _db.SaveChangesAsync();
            }

            return new IpnwMtcDataBackfillResult(records.Count, groupsProcessed, recordsUpdated);
        }

        public async Task<int> RecalculateAsync(string designation, int year)
        {
            var normalizedDesignation = designation.Trim();
            if (string.IsNullOrWhiteSpace(normalizedDesignation)) return 0;

            var records = await _db.IpnwMtcData
                .Where(x =>
                    x.Designation != null &&
                    x.Designation.Trim() == normalizedDesignation &&
                    x.Year == year.ToString())
                .ToListAsync();

            var recordsUpdated = RecalculateGroup(records);
            if (recordsUpdated > 0)
            {
                await _db.SaveChangesAsync();
            }

            return recordsUpdated;
        }

        private static int RecalculateGroup(IEnumerable<IpnwMtcData> records)
        {
            var runningScheduled = 0;
            var runningAttended = 0;
            var recordsUpdated = 0;
            int? currentCycle = null;

            foreach (var record in records.OrderBy(x => GetMonthNumber(x.Month)))
            {
                var monthNumber = GetMonthNumber(record.Month);
                if (monthNumber == 0) continue;

                // 2-month cycle: Jan-Feb=1, Mar-Apr=2, May-Jun=3, Jul-Aug=4, Sep-Oct=5, Nov-Dec=6
                var cycle = (monthNumber + 1) / 2;
                if (currentCycle != cycle)
                {
                    runningScheduled = 0;
                    runningAttended = 0;
                    currentCycle = cycle;
                }

                runningScheduled += record.Scheduled ?? 0;
                runningAttended += record.Attended ?? 0;

                if (record.CumulativeSched != runningScheduled ||
                    record.CumulativeAchieved != runningAttended)
                {
                    record.CumulativeSched = runningScheduled;
                    record.CumulativeAchieved = runningAttended;
                    recordsUpdated++;
                }
            }

            return recordsUpdated;
        }

        private static int GetMonthNumber(string? month)
        {
            if (string.IsNullOrWhiteSpace(month)) return 0;
            return MonthSequence.TryGetValue(month.Trim(), out var monthNumber) ? monthNumber : 0;
        }
    }
}
