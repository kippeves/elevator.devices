using Device.Models;

namespace Device.Interfaces;

public interface IDatabaseService
{
    Task<bool> UpdateLogWithEvent(List<ElevatorLog> list);
    Task<bool> UpdateElevatorMetaInfo(Guid ElevatorId, string Key, dynamic value);
}