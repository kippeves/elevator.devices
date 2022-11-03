using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Interfaces
{
    internal interface ILogService
    {
        public Task AddAsync(string description, string eventTypeId, bool result);
        public Task<bool> PushToDatabaseAsync();
    }
}
