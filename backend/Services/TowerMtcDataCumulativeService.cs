using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace backend.Services
{
    public class TowerMtcDataCumulativeService : ITowerMtcDataCumulativeService
    {
        private static readonly Dictionary<string, int> MonthSequence =
            CultureInfo.InvariantCulture.DateTimeFormat.MonthNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select((name, index) => new { Name = name, Number = index + 1 })
                .ToDictionary(x => x.Name, x => x.Number, StringComparer.OrdinalIgnoreCase);

        private readonly AppDbContext _db;

        public TowerMtcDataCumulativeService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<TowerMtcDataBackfillResult> RecalculateAllAsync()
        {
            var records = await _db.TowerMtcData.ToListAsync();
            var groupsProcessed = 0;
            var recordsUpdated = 0;

            var groups = records
                .Where(x => !string.IsNullOrWhiteSpace(x.Designation) && x.Year.HasValue)
                .GroupBy(x => new
                {
                    Designation = x.Designation!.Trim(),
                    Year = x.Year!.Value
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

            return new TowerMtcDataBackfillResult(records.Count, groupsProcessed, recordsUpdated);
        }

        public async Task<int> RecalculateAsync(string designation, int year)
        {
            var normalizedDesignation = designation.Trim();
            if (string.IsNullOrWhiteSpace(normalizedDesignation)) return 0;

            var records = await _db.TowerMtcData
                .Where(x =>
                    x.Designation != null &&
                    x.Designation.Trim() == normalizedDesignation &&
                    x.Year == (short)year)
                .ToListAsync();

            var recordsUpdated = RecalculateGroup(records);
            if (recordsUpdated > 0)
            {
                await _db.SaveChangesAsync();
            }

            return recordsUpdated;
        }

        private static int RecalculateGroup(IEnumerable<TowerMtcData> records)
        {
            var runningScheduled = 0;
            var runningAttended = 0;
            var currentQuarter = 0;
            var recordsUpdated = 0;

            foreach (var record in records.OrderBy(x => GetMonthNumber(x.Month)))
            {
                var monthNumber = GetMonthNumber(record.Month);
                if (monthNumber == 0) continue;

                var quarter = ((monthNumber - 1) / 3) + 1;
                if (quarter != currentQuarter)
                {
                    runningScheduled = 0;
                    runningAttended = 0;
                    currentQuarter = quarter;
                }

                runningScheduled += record.Scheduled ?? 0;
                runningAttended += record.Attended ?? 0;

                if (record.CumulativeScheduled != runningScheduled ||
                    record.CumulativeAttended != runningAttended)
                {
                    record.CumulativeScheduled = runningScheduled;
                    record.CumulativeAttended = runningAttended;
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
