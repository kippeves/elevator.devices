using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Models
{
    public class ElevatorLog
    {
        public Guid ElevatorId { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Description { get; set; }
        public string EventType { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        public ElevatorLog(Guid elevatorId, string description, string eventType, string oldValue, string newValue)
        {
            ElevatorId = elevatorId;
            TimeStamp = DateTime.Now;
            Description = description;
            EventType = eventType;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}