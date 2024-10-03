using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace ESOrleansApproach.Domain.Events
{
    [GenerateSerializer]
    public class TenantOnboarded : EventBase
    {
        [Id(0)]
        public Customer Customer { get; set; }
        [Id(1)]
        public string TenantName { get; set; }
        public TenantOnboarded(
            string aggregate,
            Guid aggregateId,
            Customer customer,
            string tenantName) : base(aggregate, aggregateId)
        {
            Customer = customer;
            TenantName = tenantName;
        }
    }
}
