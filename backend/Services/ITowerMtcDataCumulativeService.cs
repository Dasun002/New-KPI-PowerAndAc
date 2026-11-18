namespace backend.Services
{
    public interface ITowerMtcDataCumulativeService
    {
        Task<TowerMtcDataBackfillResult> RecalculateAllAsync();
        Task<int> RecalculateAsync(string designation, int year);
    }

    public sealed record TowerMtcDataBackfillResult(
        int TotalRecords,
        int GroupsProcessed,
        int RecordsUpdated);
}
