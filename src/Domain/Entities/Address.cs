using ESOrleansApproach.Domain.Common;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Entities
{
    [GenerateSerializer]
    public class Address : StateBase
    {
        [Id(0)]
        public string FirstName { get; set; }
        [Id(1)]
        public string LastName { get; set; }
        [Id(2)]
        public string Email { get; set; }
        [Id(3)]
        public string Company { get; set; }
        [Id(4)]
        public string Country { get; set; }
        [Id(5)]
        public string County { get; set; }
        [Id(6)]
        public string City { get; set; }
        [Id(7)]
        public string Address1 { get; set; }
        [Id(8)]
        public string Address2 { get; set; }
        [Id(9)]
        public string Address3 { get; set; }
        [Id(10)]
        public string Address4 { get; set; }
        [Id(11)]
        public string ZipPostalCode { get; set; }
        [Id(12)]
        public string PhoneNumber { get; set; }
        [Id(13)]
        public Guid CustomerId { get; set; }

        public override void ApplyAllLevels<T>(EventBase eventBase, T rootEntity)
        {
            var tenant = rootEntity as Tenant;
            var customer = tenant.FindCustomer(CustomerId);
            if (customer is not null)
                CallApply(customer, eventBase);
        }
        public void Apply(AddressDetailsChanged @event)
        {
            base.Apply(@event);
            FirstName = @event.Address.FirstName;
            LastName = @event.Address.LastName;
            Email = @event.Address.Email;
            Company = @event.Address.Company;
            Country = @event.Address.Country;
            City = @event.Address.City;
            Address1 = @event.Address.Address1;
            Address2 = @event.Address.Address2;
            Address3 = @event.Address.Address3;
            Address4 = @event.Address.Address4;
            ZipPostalCode = @event.Address.ZipPostalCode;
            PhoneNumber = @event.Address.PhoneNumber;
        }
        public void CreateAddress(Address address)
        {
            Id = address.Id;
            Tenant = address.Tenant;
            FirstName = address.FirstName;
            LastName = address.LastName;
            Email = address.Email;
            Company = address.Company;
            Country = address.Country;
            City = address.City;
            Address1 = address.Address1;
            Address2 = address.Address2;
            Address3 = address.Address3;
            Address4 = address.Address4;
            ZipPostalCode = address.ZipPostalCode;
            PhoneNumber = address.PhoneNumber;
        }
    }
}
