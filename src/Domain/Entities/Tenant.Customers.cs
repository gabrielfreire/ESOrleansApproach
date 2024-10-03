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
            base.Apply(@event);

            var customer = Customers.FirstOrDefault(c => c.Id == @event.CustomerId);

            if (customer is not null)
            {
                customer.Apply(@event);
            }
        }
        public void Apply(CustomerAdded @event)
        {
            base.Apply(@event);

            if (!Customers.Any(c => c.Id == @event.CustomerId))
            {
                var customer = new Customer(@event.CustomerId,
                    @event.ShoppingCartId,
                    @event.Name,
                    @event.PreferredUsername,
                    Name,
                    Id);
                customer.Apply(@event);
                Customers.Add(customer);
            }
        }

        public void Apply(CustomerRemoved @event)
        {
            base.Apply(@event);

            var _entity = FindCustomer(@event.CustomerId);

            if (_entity is not null)
            {
                Customers.Remove(_entity);
                Delete(_entity);
            }
        }
        public void Apply(AddressAdded @event)
        {
            base.Apply(@event);

            var customer = FindCustomer(@event.CustomerId);
            customer?.Apply(@event);
        }

        public void Apply(AddressRemoved @event)
        {
            base.Apply(@event);

            var customer = FindCustomer(@event.CustomerId);
            customer?.Apply(@event);
        }
        public void Apply(AddressDetailsChanged @event)
        {
            base.Apply(@event);

            var address = this.GetAddressById(@event.Address.Id);
            address?.Apply(@event);
            address?.ApplyAllLevels(@event, this);
        }

        public Customer? FindCustomer(Guid customerId) => Customers.FirstOrDefault(c => c.Id == customerId);

    }
}
