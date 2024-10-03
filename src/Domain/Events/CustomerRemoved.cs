using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Events
{
    [GenerateSerializer]
    public class CustomerRemoved : EventBase
    {
        [Id(0)]
        public Guid CustomerId { get; set; }
        public CustomerRemoved(
            string aggregate,
            Guid aggregateId,
            Guid customerId) : base(aggregate, aggregateId)
        {
            CustomerId = customerId;
        }
    }
}
