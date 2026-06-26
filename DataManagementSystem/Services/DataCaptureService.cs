using DataManagementSystem.Data;

namespace DataManagementSystem.Services;

public class DataCaptureService : IDataCaptureService
{
    private readonly DataCaptureRepository _repo;

    public DataCaptureService(DataCaptureRepository repo) => _repo = repo;

    public async Task<(bool Success, string? Error)> SaveAsync(DataCapture capture)
    {
        try
        {
            if (await _repo.IsDuplicateAsync(capture.SerialNumber, capture.CaptureDate))
                return (false, $"Serial number '{capture.SerialNumber}' already captured this month.");

            capture.Id = await _repo.InsertAsync(capture);
            Logger.LogInfo($"DataCapture saved: Id={capture.Id} Serial={capture.SerialNumber} Model={capture.ModelNumber}", "DataCaptureService");
            return (true, null);
        }
        catch (Exception ex)
        {
            Logger.LogError("SaveAsync failed", ex, "DataCaptureService");
            return (false, ex.Message);
        }
    }

    public Task<List<DataCapture>> GetRecentAsync(int count = 3)
        => _repo.GetRecentAsync(count);

    public Task<List<DataCapture>> GetFilteredAsync(string? fromDate, string? toDate, string? serial, string? model)
        => _repo.GetFilteredAsync(fromDate, toDate, serial, model);
}
