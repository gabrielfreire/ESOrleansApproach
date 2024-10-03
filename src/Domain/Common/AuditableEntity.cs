using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Common
{
    [GenerateSerializer]
    public class AuditableEntity
    {
        [Id(0)]
        public string Tenant { get; set; }
        [Id(1)]
        public string CreatedBy { get; set; }

        [Id(2)]
        public DateTime Created { get; set; }

        [Id(3)]
        public string LastModifiedBy { get; set; }

        [Id(4)]
        public DateTime LastModified { get; set; }
    }
}
