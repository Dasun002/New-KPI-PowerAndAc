namespace backend.Services
{
    public interface ISlbnMtcDataCumulativeService
    {
        Task<SlbnMtcDataBackfillResult> RecalculateAllAsync();
        Task<int> RecalculateAsync(string designation, int year);
    }

    public sealed record SlbnMtcDataBackfillResult(
        int TotalRecords,
        int GroupsProcessed,
        int RecordsUpdated);
}
