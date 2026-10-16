namespace backend.Services
{
    public interface IIpnwMtcDataCumulativeService
    {
        Task<IpnwMtcDataBackfillResult> RecalculateAllAsync();
        Task<int> RecalculateAsync(string designation, int year);
    }

    public sealed record IpnwMtcDataBackfillResult(
        int TotalRecords,
        int GroupsProcessed,
        int RecordsUpdated);
}