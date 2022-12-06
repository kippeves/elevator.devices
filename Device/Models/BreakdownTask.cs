using System;
using System.Collections.Generic;
using System.Data;
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

        public string GetReason()
        {
            return _reason;
        }

        public DateTime? GetRepairDate()
        {
            return _repairDate;
        }

        public (bool, string) GetStatus()
        {
            var status = GetRepairDate().HasValue;
            return (status, ToString());
        }

        public override string ToString()
        {
            var date = GetRepairDate();
            string dateString;
            if (!date.HasValue)
                dateString = "No";
            else dateString = "At " + date;
            return $"Reason: {_reason}, Is fixed? {dateString}";;
        }
    }
}
