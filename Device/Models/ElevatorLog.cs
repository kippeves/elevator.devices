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
        public Guid EventTypeId { get; set; }
        public bool  EventResult { get; set; }

        public ElevatorLog(Guid elevatorId, string description, string eventTypeId, bool eventResult)
        {
            ElevatorId = elevatorId;
            TimeStamp = DateTime.Now;
            Description = description;
            EventTypeId = new Guid(eventTypeId);
            EventResult = EventResult;
        }
    }
}