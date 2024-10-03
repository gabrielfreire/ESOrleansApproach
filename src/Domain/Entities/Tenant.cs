using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Orleans.Runtime.UniqueKey;
using static System.Formats.Asn1.AsnWriter;

namespace ESOrleansApproach.Domain.Entities
{
    [GenerateSerializer]
    public partial class Tenant : StateBase
    {
        [Id(0)]
        public string Name { get; set; }
        [Id(1)]
        public bool IsTenant { get; set; }
        [Id(2)]
        public bool Active { get; set; }
        [Id(4)]
        public List<Customer> Customers { get; set; } = [];

        public void Apply(TenantOnboarded @event)
        {
            base.Apply(@event);

            if (!string.IsNullOrEmpty(@event.TenantName))
            {
                Name = @event.TenantName;
            }

            Id = @event.AggregateId;
            IsTenant = true;
            Active = true;

            @event.Customer.Tenant = Name;

            var _customer = new Customer(@event.Customer.Id,
                @event.Customer.ShoppingCart.Id,
                @event.Customer.Name,
                @event.Customer.PreferredUsername,
                Name,
                Id);

            _customer.Apply(@event);

            if (!Customers.Any(s => s.Id == _customer.Id))
                Customers.Add(_customer);

        }
    }
}
