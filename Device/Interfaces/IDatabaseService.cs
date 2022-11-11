using Device.Models;

namespace Device.Interfaces;

public interface IDatabaseService
{
    Task<(bool status, string data)> GetConnectionstringForIdAsync(Guid id);
    public Task<(bool status, string data)> UpdateConnectionStringForElevatorByIdAsync(Guid deviceId);
    Task<List<DeviceInfo>> GetAllElevators();
    Task<bool> UpdateLogWithEvent(List<ElevatorLog> list);
    Task<bool> UpdateElevator(Guid id, Dictionary<string, dynamic> changedValues);
    public Task<(bool status, string message, DeviceInfo? data)> GetElevatorByIdAsync(Guid deviceId);
    public Task<(bool status, string message, Dictionary<string, dynamic?>? data)> LoadMetadataForElevatorByIdAsync(Guid deviceId);
    public Task<(bool status, string message)> SetFunctionalityInDbById(Guid id, string value);
    public Task<bool> RemoveListOfMetaData(Guid deviceId, List<string> keys);
}