using Microsoft.Azure.Amqp.Framing;
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
        public long TimeStamp { get; set; }
        public string Description { get; set; }
        public string EventType { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        public ElevatorLog(Guid elevatorId, string description, string eventType, string oldValue, string newValue)
        {
            var date = DateTime.Now;
            var zero = new DateTime(1970, 1, 1);
            var span = date.Subtract(zero);

            ElevatorId = elevatorId;
            TimeStamp = (long)span.TotalMilliseconds;
            Description = description;
            EventType = eventType;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}