using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartApp.CLI.Device.Models
{
    internal class DeviceInfo
    {
        public DeviceInfo()
        {
        }

        public DeviceInfo(string deviceId)
        {
            DeviceId = new Guid(deviceId);
        }

        public Guid DeviceId { get; set; }
        public Guid CompanyId { get; set; }
        public Guid BuildingId { get; set; }
        public Guid ElevatorTypeId { get; set; }
        public bool IsFunctioning { get; set; }
        public Dictionary<string, dynamic?> Device { get; set; } = new()
        {
            ["Name"] = null,
            ["CompanyName"] = null,
            ["BuildingName"] = null,
            ["ElevatorType"] = null,
            ["Interval"] = (int)TimeSpan.FromMinutes(1).TotalMilliseconds
        };

        public Dictionary<string, dynamic> Meta { get; set; }
    }
}
