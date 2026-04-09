using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Entities
{
    public partial class Tenant
    {
        public void Apply(ShoppingCartItemAdded @event)
        {
            var customer = FindCustomer(@event.CustomerId);
            customer?.ShoppingCart?.Apply(@event);
        }
        public void Apply(ShoppingCartItemRemoved @event)
        {
            var shoppingCartItem = this.GetShoppingCartItemById(@event.ShoppingCartItemId);

            if (shoppingCartItem is not null)
            {
                var shoppingCart = this.GetShoppingCartById(shoppingCartItem.ShoppingCartId);
                shoppingCart?.Apply(@event);
            }
        }
    }
}
