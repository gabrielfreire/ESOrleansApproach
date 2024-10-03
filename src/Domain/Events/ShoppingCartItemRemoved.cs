using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Events
{
    [GenerateSerializer]
    public class ShoppingCartItemRemoved : EventBase
    {
        [Id(0)]
        public int Quantity { get; set; }

        [Id(1)]
        public Guid ShoppingCartItemId { get; set; }

        [Id(2)]
        public ShoppingCartItem ShoppingCartItem { get; set; }
        public ShoppingCartItemRemoved(
            string aggregate,
            Guid aggregateId,
            int quantity,
            Guid shoppingCartItemId,
            ShoppingCartItem shoppingCartItem) : base(aggregate, aggregateId)
        {
            Quantity = quantity;
            ShoppingCartItemId = shoppingCartItemId;
            ShoppingCartItem = shoppingCartItem;
        }
    }
}
