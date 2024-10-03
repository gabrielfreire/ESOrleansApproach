namespace ESOrleansApproach.Domain.Events
{
    [GenerateSerializer]
    public class AddressAdded : EventBase
    {
        [Id(0)]
        public Guid CustomerId { get; set; }
        [Id(1)]
        public Address Address { get; set; }
        public AddressAdded(
            string aggregate,
            Guid aggregateId,
            Guid customerId,
            Address address) : base(aggregate, aggregateId)
        {
            CustomerId = customerId;
            Address = address;
        }
    }
}
