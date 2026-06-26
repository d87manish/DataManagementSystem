namespace DataManagementSystem.Services;

public interface IDataCaptureService
{
    Task<(bool Success, string? Error)> SaveAsync(DataCapture capture);
    Task<List<DataCapture>> GetRecentAsync(int count = 3);
    Task<List<DataCapture>> GetFilteredAsync(string? fromDate, string? toDate, string? serial, string? model);
}
