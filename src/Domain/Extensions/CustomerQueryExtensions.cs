using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Extensions
{
    public static class CustomerQueryExtensions
    {
        public static List<ShoppingCart> SearchShoppingCarts(this Tenant tenant, ICollection<Tuple<string, object>> queries = null)
        {
            var queryable = tenant.Customers.Select(c => c.ShoppingCart)
                .OrderByDescending(o => o.UpdatedOnUtc)
                .OrderByDescending(o => o.CreatedOnUtc).AsQueryable();

            return queryable.GetQueryable(queries)
                .ToList();
        }
        public static int CountShoppingCarts(this Tenant tenant, ICollection<Tuple<string, object>> queries = null)
        {
            var queryable = tenant.Customers.Select(c => c.ShoppingCart).AsQueryable();

            return queryable.GetQueryable(queries)
                .Count();
        }
        public static ShoppingCart GetShoppingCartById(this Tenant tenant, Guid shoppingCartId)
        {
            return tenant.Customers.Select(c => c.ShoppingCart).FirstOrDefault(c => c.Id == shoppingCartId);
        }
        public static ShoppingCartItem GetShoppingCartItemById(this Tenant tenant, Guid shoppingCartItemId)
        {
            return tenant.Customers.Select(c => c.ShoppingCart).SelectMany(s => s.Items).FirstOrDefault(c => c.Id == shoppingCartItemId);
        }
        public static List<Address> SearchAddresses(this Tenant tenant, ICollection<Tuple<string, object>> queries = null)
        {
            var queryable = tenant.Customers.SelectMany(c => c.Addresses)
                .OrderByDescending(o => o.UpdatedOnUtc)
                .OrderByDescending(o => o.CreatedOnUtc).AsQueryable();

            return queryable.GetQueryable(queries)
                .ToList();
        }
        public static int CountAddresses(this Tenant tenant, ICollection<Tuple<string, object>> queries = null)
        {
            var queryable = tenant.Customers.SelectMany(c => c.Addresses).AsQueryable();

            return queryable.GetQueryable(queries)
                .Count();
        }
        public static Address GetAddressById(this Tenant tenant, Guid addressId)
        {
            return tenant.Customers.SelectMany(c => c.Addresses).FirstOrDefault(a => a.Id == addressId);
        }
        public static List<Address> SearchAddresses(this Tenant tenant, Guid customerId, ICollection<Tuple<string, object>> queries = null)
        {
            var customer = tenant.FindCustomer(customerId);

            if (customer is not null)
            {
                var queryable = customer.Addresses
                    .OrderByDescending(o => o.UpdatedOnUtc)
                    .OrderByDescending(o => o.CreatedOnUtc).AsQueryable();
                return queryable.GetQueryable(queries)
                    .ToList();
            }
            return [];

        }
    }
}
