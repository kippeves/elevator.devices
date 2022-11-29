using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Device.Models
{
    public class Breakdown
    {
        private Guid _id;
        private readonly List<BreakdownTask> _subTasks = new();
        private DateTime _createdAt;
        private DateTime? _finalRepairdate;

        public Breakdown(Guid id, List<BreakdownTask> subTasks, DateTime createdAt)
        {
            _id = id;
            _subTasks = subTasks;
            _createdAt = createdAt;
        }

        public Breakdown(List<string> reasons)
        {
            foreach(var reason in reasons)
            {
                _subTasks!.Add(new BreakdownTask(reason));
            }
            _createdAt = DateTime.Now;
        }

        public BreakdownTask GetReason(Guid id)
        {
            return _subTasks.Find(sub => sub.GetId() == id);
        }

        public async Task<List<BreakdownTask>> GetTaskList()
        {
            return await Task.FromResult(_subTasks.ToList());
        }

        public void SetFixed()
        {
            _finalRepairdate = DateTime.Now;
        }

        public DateTime? GetFinishDate()
        {
            return _finalRepairdate;
        }

        public bool AreAllSubtasksFixed()
        {
            return _subTasks.Any(s => !s.GetRepairStatus().Status);
        }

        public bool RepairPart(BreakdownTask task)
        {
            var reason = _subTasks.SingleOrDefault(task);
            return reason != default && reason.Fix();
        }
    }
}
