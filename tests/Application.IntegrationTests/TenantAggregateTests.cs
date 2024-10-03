using ESOrleansApproach.Domain.Entities;
using ESOrleansApproach.GrainInterfaces;
using ESOrleansApproach.Grains;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.IntegrationTests
{
    using static Testing;
    public class TenantAggregateTests
    {
        Tenant tenant = null;

        Guid tenantId = Guid.NewGuid();


        [Test, Order(1)]
        public async Task N0_Activate()
        {
            var _grain = _cluster.GrainFactory.GetGrain<ITenantAggregate>(tenantId);
            _grain.Should().NotBeNull();
        }

        [Test, Order(2)]
        public async Task N1_OnboardTenant()
        {
            var _grain = _cluster.GrainFactory.GetGrain<ITenantAggregate>(tenantId);
            tenant = await _grain.OnboardTenant("John Test", "johntest@test.com", "tootle");

            tenant.Should().NotBeNull();
            tenant.Customers.Should().HaveCount(1);
            tenant.Name.Should().Be("tootle");
            var customer = tenant.Customers[0];
            customer.Should().NotBeNull();
            customer.Name.Should().Be("John Test");
            customer.PreferredUsername.Should().Be("johntest@test.com");
            customer.ShoppingCart.Should().NotBeNull();

        }

        [Test, Order(3)]
        public async Task N2_Should_Add_Edit_Delete_Addresses()
        {
            var _grain = _cluster.GrainFactory.GetGrain<ITenantAggregate>(tenantId);
            tenant = await _grain.GetCurrentState();

            // create
            var addressId = await _grain.AddCustomerAddress(new Address()
            {
                FirstName = "Test",
                LastName = "Doe",
                Email = "testdoe@test.com",
                Address1 = "29 Grove Road",
                Address2 = "A52C8T0"
            }, tenant.Customers[0].Id);

            var customer = await _grain.GetCustomerById(tenant.Customers[0].Id);
            customer.Addresses.Should().HaveCount(1);

            // update
            var addressToUpdate = customer.Addresses[0];
            addressToUpdate.LastName = "Doe updated";
            await _grain.UpdateCustomerAddress(addressToUpdate, tenant.Customers[0].Id);

            customer = await _grain.GetCustomerById(tenant.Customers[0].Id);

            var updatedAddress= customer.Addresses[0];
            updatedAddress.LastName.Should().Be("Doe updated");

            // delete
            await _grain.RemoveCustomerAddress(updatedAddress.Id, customer.Id);

            customer = await _grain.GetCustomerById(tenant.Customers[0].Id);
            customer.Addresses.Should().HaveCount(0);
        }

        [Test, Order(4)]
        public async Task N3_Should_Add_Edit_Delete_Customers()
        {
            var _grain = _cluster.GrainFactory.GetGrain<ITenantAggregate>(tenantId);
            tenant = await _grain.GetCurrentState();

            var customer1 = await _grain.CreateCustomer("Tul", "tul@gmail.com");
            customer1.Should().NotBeNull();
            customer1.TenantId.Should().Be(tenant.Id);
            customer1.Name.Should().Be("Tul");
            customer1.PreferredUsername.Should().Be("tul@gmail.com");
            customer1.ShoppingCart.Should().NotBeNull();

            var customer2 = await _grain.CreateCustomer("Cul", "cul@gmail.com");
            customer2.Should().NotBeNull();
            customer2.TenantId.Should().Be(tenant.Id);
            customer2.Name.Should().Be("Cul");
            customer2.PreferredUsername.Should().Be("cul@gmail.com");
            customer2.ShoppingCart.Should().NotBeNull();

            await _grain.UpdateCustomer(customer1.Id, "Zul", "zul@gmail.com");
            customer1 = await _grain.GetCustomerById(customer1.Id);
            customer1.Name.Should().Be("Zul");
            customer1.PreferredUsername.Should().Be("zul@gmail.com");

            await _grain.RemoveCustomer(customer2.Id);
            customer2 = await _grain.GetCustomerById(customer2.Id);
            customer2.Should().BeNull();
        }
    }
}
