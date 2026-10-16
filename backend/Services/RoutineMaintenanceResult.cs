namespace backend.Services
{
    public record RoutineMaintenanceResult(
        string Designation,
        string NormalizedAreaCode,
        decimal Percentage,
        decimal NodesCount = 0m);
}
