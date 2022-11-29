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
        public string CreateAccident(List<string> reasons);
        public void PreloadFromDatabaseEntry();
        public bool IsBroken();
    }
}
