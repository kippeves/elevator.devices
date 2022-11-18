namespace Device.Models
{
    public class DeviceInfo
    {
        public DeviceInfo()
        {
        }

        public DeviceInfo(Guid id)
        {
            DeviceId = id;
        }

        public Guid DeviceId { get; set; }
        public Guid CompanyId { get; set; }
        public Guid BuildingId { get; set; }
        public Guid ElevatorTypeId { get; set; }
        public bool IsFunctioning { get; set; }
        public Dictionary<string, dynamic?> Device { get; set; } = new()
        {
            ["DeviceName"] = null,
            ["CompanyName"] = null,
            ["BuildingName"] = null,
            ["ElevatorType"] = null,
            ["MaxFloor"] = null,
            ["Interval"] = (int)TimeSpan.FromMinutes(1).TotalMilliseconds
        };

        public Dictionary<string, dynamic?> Meta { get; set; }
    }
}
