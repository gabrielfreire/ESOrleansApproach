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
        public Task AddItemToShoppingCart(Guid productId, int quantity, Guid customerId)
        {
            return RaiseEventAsync(new ShoppingCartItemAdded(aggrName, this.GetPrimaryKey(),
                productId, quantity, 0, customerId, Guid.NewGuid()));
        }

        public Task<ShoppingCart> GetShoppingCartById(Guid shoppingCartId)
        {
            return Task.FromResult(State.GetShoppingCartById(shoppingCartId));
        }

        public Task<int> GetShoppingCartsCount(ICollection<Tuple<string, object>> query = null)
        {
            return Task.FromResult(State.CountShoppingCarts(query));
        }

        public Task RemoveItemFromShoppingCart(int quantity, Guid shoppingCartItemId)
        {
            var shoppingCartItem = State.GetShoppingCartItemById(shoppingCartItemId);
            return RaiseEventAsync(new ShoppingCartItemRemoved(aggrName, this.GetPrimaryKey(), quantity, 
                shoppingCartItemId, 
                shoppingCartItem));
        }

        public Task<List<ShoppingCart>> SearchShoppingCarts(ICollection<Tuple<string, object>> query = null)
        {
            return Task.FromResult(State.SearchShoppingCarts(query));
        }

    }
}
