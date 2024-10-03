using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Common
{
    public class RemoteLogDetail
    {
        public string LogLevel { get; set; }
        public string ShortMessage { get; set; }
        public string FullMessage { get; set; }
        public string IPAddress { get; set; }
    }
}
