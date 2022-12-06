using Device.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Interfaces
{
    internal interface IRepairService
    {
        public bool FinishRepair();
        public bool FixPart(Guid partId);
        public Breakdown GetBreakdown();
        public (string description, Breakdown breakdown) CreateAccident(List<string> reasons);
        public void PreloadFromDatabaseEntry(Breakdown b);
        public bool IsWorking();
        public Task<List<BreakdownTask>> GetTaskList();
        bool CheckIfAllTasksAreDone();
    }
}
