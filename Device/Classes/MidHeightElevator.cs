using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Device.Classes.Base;
using Device.Models;
using SmartApp.CLI.Device.Models;

namespace Device.Classes
{
    internal class MidHeightElevator : Elevator
    {
        public MidHeightElevator(DeviceInfo info) : base(info)
        {
        }
    }
}
