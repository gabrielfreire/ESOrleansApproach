using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Events
{
    [GenerateSerializer]
    public class ShoppingCartItemAdded : EventBase
    {
        [Id(0)]
        public Guid ProductId { get; set; }
        [Id(1)]
        public int Quantity { get; set; }
        [Id(2)]
        public decimal Price { get; set; }
        [Id(4)]
        public Guid CustomerId { get; set; }
        [Id(5)]
        public Guid ShoppingCartItemId { get; set; }
        public ShoppingCartItemAdded(
            string aggregate,
            Guid aggregateId,
            Guid productId,
            int quantity,
            decimal price,
            Guid customerId,
            Guid shoppingCartItemId) : base(aggregate, aggregateId)
        {
            ProductId = productId;
            Quantity = quantity;
            Price = price;
            CustomerId = customerId;
            ShoppingCartItemId = shoppingCartItemId;
        }
    }
}
