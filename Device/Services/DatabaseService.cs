using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Services
{
    internal class DatabaseService : IDatabaseService
    {
        public Task UpdateLogWithEvent(Guid ElevatorId, string Description, string EventType, bool EventResult)
        {
            return Task.CompletedTask;
        }

        public Task<bool> UpdateElevatorMetaInfo(Guid ElevatorId, string Key, dynamic value)
        {
            return Task.FromResult(true);
        }
    }
}
