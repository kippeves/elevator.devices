using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Device.Classes.Base;
using Device.Interfaces;
using Device.Models;

namespace Device.Services
{
    internal class LogService : ILogService
    {
        private List<ElevatorLog> _logs;
        private IDatabaseService _databaseService;
        private Guid _id;

        public LogService(Guid id, IDatabaseService databaseService)
        {
            _id = id;
            _databaseService = databaseService;
            _logs = new List<ElevatorLog>();
        }

        public Task AddAsync(string description, string eventTypeId, string oldValue, string newValue)
        {
            var logEntry = new ElevatorLog(_id, description, eventTypeId, oldValue, newValue);
            _logs.Add(logEntry);
            return Task.CompletedTask;
        }

        public async Task<bool> PushToDatabaseAsync()
        {
            var result = await _databaseService.UpdateLogWithEvent(_logs);
            if (result) _logs.Clear();
            return result;
        }
    }
}
