using ESOrleansApproach.Domain.Entities;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.GrainInterfaces
{
    public interface ITenantAggregate : IGrainWithGuidKey
    {
        Task<Tenant> GetCurrentState();
        Task<Tenant> OnboardTenant(string customerName, string customerEmail, string tenantName);
        Task<Customer> CreateCustomer(string name, string email);
        Task UpdateCustomer(Guid custoemrId, string name, string email);
        Task<Customer> GetCustomerById(Guid customerId);
        Task RemoveCustomer(Guid customerId);
        Task<List<Customer>> SearchCustomers(ICollection<Tuple<string, object>> query = null);
        Task<int> GetCustomersCount(ICollection<Tuple<string, object>> query = null);
        Task<Guid> AddCustomerAddress(Address address, Guid customerId);
        Task UpdateCustomerAddress(Address address, Guid customerId);
        Task RemoveCustomerAddress(Guid addressId, Guid customerId);
        Task AddItemToShoppingCart(Guid productId, int quantity, Guid customerId);
        Task RemoveItemFromShoppingCart(int quantity, Guid shoppingCartItemId);
        Task<ShoppingCart> GetShoppingCartById(Guid shoppingCartId);
        Task<List<ShoppingCart>> SearchShoppingCarts(ICollection<Tuple<string, object>> query = default);
        Task<int> GetShoppingCartsCount(ICollection<Tuple<string, object>> query = default);
    }
}
