namespace Device.Services;

public interface IDatabaseService
{
    Task UpdateLogWithEvent(Guid ElevatorId, string Description, string EventType, bool EventResult);
    Task<bool> UpdateElevatorMetaInfo(Guid ElevatorId, string Key, dynamic value);
}
