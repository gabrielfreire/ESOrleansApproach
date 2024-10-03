namespace ESOrleansApproach.Domain.Events
{
    [GenerateSerializer]
    public class AddressRemoved : EventBase
    {
        [Id(0)]
        public Guid CustomerId { get; set; }
        [Id(1)]
        public Guid AddressId { get; set; }
        public AddressRemoved(
            string aggregate,
            Guid aggregateId,
            Guid customerId,
            Guid addressId) : base(aggregate, aggregateId)
        {
            CustomerId = customerId;
            AddressId = addressId;
        }
    }
}
