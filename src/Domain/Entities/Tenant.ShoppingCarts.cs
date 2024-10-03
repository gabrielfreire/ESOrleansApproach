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
            base.Apply(@event);

            var customer = FindCustomer(@event.CustomerId);
            customer?.ShoppingCart.Apply(@event);
            customer?.ShoppingCart.ApplyAllLevels(@event, this);
        }
        public void Apply(ShoppingCartItemRemoved @event)
        {
            base.Apply(@event);

            var shoppingCartItem = this.GetShoppingCartItemById(@event.ShoppingCartItemId);

            if (shoppingCartItem is not null)
            {
                var shoppingCart = this.GetShoppingCartById(shoppingCartItem.ShoppingCartId);
                shoppingCart?.Apply(@event);
                shoppingCart?.ApplyAllLevels(@event, this);
            }
        }
    }
}
