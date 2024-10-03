using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Events
{
    [GenerateSerializer]
    public class CustomerAdded : EventBase
    {
        [Id(0)]
        public Guid CustomerId { get; set; }
        [Id(1)]
        public Guid ShoppingCartId { get; set; }
        [Id(2)]
        public string Name { get; set; }
        [Id(3)]
        public string PreferredUsername { get; set; }
        public CustomerAdded(
            string aggregate,
            Guid aggregateId,
            string name,
            string preferredUsername,
            Guid customerId,
            Guid shoppingCartId) : base(aggregate, aggregateId)
        {
            Name = name;
            PreferredUsername = preferredUsername;
            CustomerId = customerId;
            ShoppingCartId = shoppingCartId;
        }
    }
}
