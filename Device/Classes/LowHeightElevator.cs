using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Device.Classes.Base;
using SmartApp.CLI.Device.Models;

namespace Device.Classes
{
    internal class LowHeightElevator : Elevator
    {
        public LowHeightElevator(DeviceInfo info) : base(info)
        {
        }

        protected override Task UpdateReportedProperties()
        {
            return Task.CompletedTask;
        }
    }
}
