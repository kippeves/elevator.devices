using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Models
{
    internal class UpdateMetaDataRequest
    {
        public KeyValuePair<string,dynamic> Values { get; set; }
    }
}
