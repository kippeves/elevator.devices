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
<<<<<<< HEAD
        public bool EventResult { get; set; }

        public ElevatorLog(Guid elevatorId, string description, string eventType, bool eventResult)
=======
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        public ElevatorLog(Guid elevatorId, string description, string eventType, string oldValue, string newValue)
>>>>>>> KristianV
        {
            ElevatorId = elevatorId;
            TimeStamp = DateTime.Now;
            Description = description;
            EventType = eventType;
<<<<<<< HEAD
            EventResult = EventResult;
=======
            OldValue = oldValue;
            NewValue = newValue;
>>>>>>> KristianV
        }
    }
}