using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Models
{
    public class BreakdownTask
    {
        private readonly Guid _id;
        private readonly string _reason;
        private DateTime? _repairDate = null;

        public BreakdownTask(string reason)
        {
            _id = new Guid();
            _reason = reason;
        }

        public BreakdownTask(Guid id, string reason, DateTime? repairDate)
        {
            _id = id;
            _reason = reason;
            _repairDate = repairDate;
        }

        public Guid GetId()
        {
            return _id;
        }

        public bool Fix()
        {
            _repairDate = DateTime.Now;
            return _repairDate.HasValue;
        }

        public (bool Status, object Date)GetRepairStatus()
        {
            return (_repairDate.HasValue, _repairDate);
        }

        public override string ToString()
        {
            var (status, date) = GetRepairStatus();
            return $"Reason: {_reason}, Is fixed? {(status ? "At " + date : "No")}";
        }
    }
}
