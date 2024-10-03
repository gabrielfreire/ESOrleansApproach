using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Entities
{
    [GenerateSerializer]
    public class ShoppingCartItem : StateBase
    {
        [Id(0)]
        public Guid ProductId { get; init; }

        [Id(1)]
        public int Quantity { get; private set; }
        [Id(3)]
        public decimal Price { get; init; }
        [Id(2)]
        public Guid ShoppingCartId { get; init; }

        private ShoppingCartItem() { }
        public ShoppingCartItem(
            Guid shoppingCartItemId,
            Guid productId,
            int quantity,
            decimal price,
            Guid shoppingCartId)
        {
            Id = shoppingCartItemId;
            ProductId = productId;
            Quantity = quantity;
            ShoppingCartId = shoppingCartId;
            Price = Math.Round(price, 2, MidpointRounding.AwayFromZero);
        }

        public void Apply(ShoppingCartItemRemoved @event)
        {
            base.Apply(@event);

            Quantity -= @event.Quantity;
        }
        public void Apply(ShoppingCartItemAdded @event)
        {
            base.Apply(@event);

            Quantity += @event.Quantity;
        }

    }
}
