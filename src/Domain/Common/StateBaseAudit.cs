using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Common
{
    [GenerateSerializer]
    public class StateBaseAudit
    {
        [Id(0)]
        public string Tenant { get; set; }
        [Id(1)]
        public string CreatedBy { get; set; }
        [Id(2)]
        public string UpdatedBy { get; set; }
        [Id(3)]
        public DateTimeOffset CreatedOnUtc { get; set; }
        [Id(4)]
        public DateTimeOffset UpdatedOnUtc { get; set; }

    }
}
