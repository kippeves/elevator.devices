using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Interfaces
{
    internal interface IChangeService
    {
        public Task SetChanged(string key);
        public Task ClearChanges();
        public Task<List<string>> GetChanged();
        public Task<bool> HasAnyChangesBeenDone();
    }
}
