using ESOrleansApproach.Domain.Entities;
using ESOrleansApproach.Domain.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Grains
{
    public partial class TenantAggregate
    {
        public async Task<Customer> CreateCustomer(string name, string email)
        {
            var customerId = Guid.NewGuid();
            
            await RaiseEventAsync(new CustomerAdded(aggrName, this.GetPrimaryKey(),
                name, email, customerId, Guid.NewGuid()));

            return await GetCustomerById(customerId);
        }
        public Task RemoveCustomer(Guid customerId)
        {
            return RaiseEventAsync(new CustomerRemoved(aggrName, this.GetPrimaryKey(), customerId));
        }
        public Task<Customer> GetCustomerById(Guid customerId)
        {
            return Task.FromResult(State.FindCustomer(customerId));
        }

        public Task<int> GetCustomersCount(ICollection<Tuple<string, object>> query = null)
        {
            return Task.FromResult(State.CountCustomers(query));
        }
        public async Task<Guid> AddCustomerAddress(Address address, Guid customerId)
        {
            address.Id = Guid.NewGuid();
            await RaiseEventAsync(new AddressAdded(aggrName, this.GetPrimaryKey(), customerId, address));
            return address.Id;
        }
        public async Task RemoveCustomerAddress(Guid addressId, Guid customerId)
        {
            await RaiseEventAsync(new AddressRemoved(aggrName, this.GetPrimaryKey(), customerId, addressId));
        }
        public Task<List<Customer>> SearchCustomers(ICollection<Tuple<string, object>> query = null)
        {
            return Task.FromResult(State.SearchCustomers(query));
        }
        public async Task UpdateCustomer(Guid customerId, string name, string email)
        {
            await RaiseEventAsync(new CustomerDetailsChanged(aggrName, this.GetPrimaryKey(), 
                name, email, customerId));
        }

        public async Task UpdateCustomerAddress(Address address, Guid customerId)
        {
            await RaiseEventAsync(new AddressDetailsChanged(aggrName, this.GetPrimaryKey(), customerId, address));
        }
    }
}
