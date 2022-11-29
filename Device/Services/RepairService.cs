using Device.Interfaces;
using Device.Models;

namespace Device.Services;

internal class RepairService : IRepairService
{
    private readonly ILogService _logService;

    public Breakdown? CurrentBreakdown;
    private readonly Guid _deviceId;

    public RepairService(ILogService logService, Guid deviceId)
    {
        _logService = logService;
        _deviceId = deviceId;
    }

    public string CreateAccident(List<string> reasons)
    {
        CurrentBreakdown = new Breakdown(reasons);
        var description = $"Elevator {_deviceId} has broken down at {DateTime.Now}. Reasons: ";
        reasons.ForEach(reason => { description += reason != reasons.Last() ? reason + ", " : reason + "."; });
        _logService.AddAsync(description, "Breakdown", "NotBroken",
            "Broken");
        return description;
    }

    public void PreloadFromDatabaseEntry(Breakdown breakdown)
    {
        CurrentBreakdown = breakdown;
    }

    public bool IsBroken()
    {
        return CurrentBreakdown != null;
    }

    public bool FixPart(Guid partId)
    {
        var task = CurrentBreakdown?.GetReason(partId);
        if (task == null) return false;

        if (!CurrentBreakdown.RepairPart(task)) return false;
        _logService.AddAsync($"Part-task \"{task}\" is completed at {DateTime.Now}", "PartRepair", "NotFixed", "Fixed");
        return true;
    }

    public Breakdown GetBreakdown()
    {
        return CurrentBreakdown;
    }

    public bool FinishRepair()
    {
        if (CurrentBreakdown == null) return false;
        CurrentBreakdown.SetFixed();
        _logService.AddAsync($"Repair finished at {CurrentBreakdown.GetFinishDate()}", "FinishRepair", "NotFinished",
            "Finished");
        CurrentBreakdown = null;
        return true;
    }
}