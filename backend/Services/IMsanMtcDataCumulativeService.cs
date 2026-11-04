namespace backend.Services
{
    public interface IMsanMtcDataCumulativeService
    {
        Task<MsanMtcDataBackfillResult> RecalculateAllAsync();
        Task<int> RecalculateAsync(string designation, int year);
    }

    public sealed record MsanMtcDataBackfillResult(
        int TotalRecords,
        int GroupsProcessed,
        int RecordsUpdated);
}
