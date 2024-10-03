using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Events
{
    [GenerateSerializer]
    public class CustomerDetailsChanged : EventBase
    {
        [Id(0)]
        public string Name { get; set; }
        [Id(1)]
        public string PreferredUsername { get; set; }
        [Id(2)]
        public Guid CustomerId { get; set; }
        public CustomerDetailsChanged(
            string aggregate,
            Guid aggregateId,
            string name,
            string preferredUsername,
            Guid customerId) : base(aggregate, aggregateId)
        {
            Name = name;
            PreferredUsername = preferredUsername;
            CustomerId = customerId;
        }

    }
}
