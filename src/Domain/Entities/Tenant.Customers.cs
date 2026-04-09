using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Entities
{
    public partial class Tenant
    {
        public void Apply(CustomerDetailsChanged @event)
        {
            var customer = Customers.FirstOrDefault(c => c.Id == @event.CustomerId);

            if (customer is not null)
            {
                customer.Apply(@event);
            }
        }
        public void Apply(CustomerAdded @event)
        {
            if (!Customers.Any(c => c.Id == @event.CustomerId))
            {
                var customer = new Customer(@event.CustomerId,
                    @event.ShoppingCartId,
                    @event.Name,
                    @event.PreferredUsername,
                    Name,
                    Id);
                Subscribe(customer);
                Customers.Add(customer);
                base.Apply(@event);
            }
        }

        public void Apply(CustomerRemoved @event)
        {
            var _entity = FindCustomer(@event.CustomerId);

            if (_entity is not null)
            {
                Unsubscribe(_entity);
                Customers.Remove(_entity);
                base.Apply(@event);
            }
        }
        public void Apply(AddressAdded @event)
        {
            var customer = FindCustomer(@event.CustomerId);
            customer?.Apply(@event);
        }

        public void Apply(AddressRemoved @event)
        {
            var customer = FindCustomer(@event.CustomerId);
            customer?.Apply(@event);
        }
        public void Apply(AddressDetailsChanged @event)
        {
            var address = this.GetAddressById(@event.Address.Id);
            if (address is not null)
            {
                address.Apply(@event);
            }
        }

        public Customer FindCustomer(Guid customerId) => Customers.FirstOrDefault(c => c.Id == customerId);

    }
}
