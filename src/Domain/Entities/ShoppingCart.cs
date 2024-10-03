using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ESOrleansApproach.Domain.Entities
{
    [GenerateSerializer]
    public class ShoppingCart : StateBase
    {
        [Id(0)]
        private List<ShoppingCartItem> _items = [];
        public List<ShoppingCartItem> Items { get => _items; }
        [Id(1)]
        public Guid CustomerId { get; private set; }

        private ShoppingCart() { }
        public ShoppingCart(Guid shoppingCartId, Guid customerId, string tenant)
        {
            Id = shoppingCartId;
            CustomerId = customerId;
            _items = [];
            Tenant = tenant;
        }

        public void Apply(ShoppingCartItemAdded @event)
        {
            base.Apply(@event);

            var existingItem = Items.Where(i => i.ProductId == @event.ProductId).FirstOrDefault();

            if (existingItem is not null)
            {
                existingItem.Apply(@event);
            }
            else
            {
                var item = new ShoppingCartItem(@event.ShoppingCartItemId,
                    @event.ProductId,
                    @event.Quantity,
                    @event.Price,
                    Id);

                _items.Add(item);
            }
        }
        public void Apply(ShoppingCartItemRemoved @event)
        {
            base.Apply(@event);

            var existingItem = FindItem(@event.ShoppingCartItem.ProductId);
            if (existingItem is not null && !existingItem.Deleted)
            {
                if (existingItem.Quantity > @event.Quantity)
                {
                    existingItem.Apply(@event);
                }
                else
                {
                    _items.Remove(existingItem);
                    Delete(existingItem);
                }
            }
        }
        public override void ApplyAllLevels<T>(EventBase eventBase, T rootEntity)
        {
            var tenant = rootEntity as Tenant;
            var customer = tenant.FindCustomer(CustomerId);
            if (customer is not null)
                CallApply(customer, eventBase);
        }
        public bool IsEmpty() => !Items.Any();
        public ShoppingCartItem? FindItem(Guid productId) => Items.FirstOrDefault(i => i.ProductId == productId);
        public ShoppingCartItem? FindItem(Guid productId, decimal price) => Items.FirstOrDefault(i => i.ProductId == productId && i.Price == price);
    }
}
