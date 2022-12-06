using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Device.Interfaces;

namespace Device.Services
{
    internal class ChangeService: IChangeService
    {
        private Dictionary<string, bool> _hasChanged;
        public ChangeService(List<string> deviceKeys)
        {
            _hasChanged = new Dictionary<string, bool>();
            deviceKeys.ForEach(row =>
            {
                _hasChanged.Add(row, false);
            });
        }

        public async Task<List<string>> GetChanged()
        {
            return await Task.FromResult(_hasChanged.Where(row => row.Value == true).Select(item => item.Key).ToList());
        }

        public async Task SetChanged(string key)
        {
            _hasChanged[key] = true;
        }

        public async Task ClearChanges()
        {
            foreach (var row in _hasChanged)
                _hasChanged[row.Key] = false;
        }

        public async Task<bool> HasAnyChangesBeenDone()
        {
            return _hasChanged.ContainsValue(true);
        }
    }
}
