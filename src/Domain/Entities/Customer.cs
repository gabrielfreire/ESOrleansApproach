namespace ESOrleansApproach.Domain.Entities
{
    [GenerateSerializer]
    public class Customer : StateBase
    {
        [Id(0)]
        public string Name { get; set; }
        [Id(1)]
        public List<Address> Addresses { get; set; } = [];
        [Id(3)]
        public string PreferredUsername { get; set; }
        [Id(4)]
        public DateTimeOffset? DateOfBirth { get; set; }
        [Id(5)]
        public string Gender { get; set; }
        [Id(6)]
        public ShoppingCart ShoppingCart { get; set; }
        [Id(7)]
        public Guid TenantId { get; set; }

        private Customer() { }
        public Customer(
            Guid id,
            Guid shoppingCartId,
            string name,
            string? email,
            string tenant,
            Guid tenantId)
        {
            Id = id;
            Name = name;
            PreferredUsername = email;
            Tenant = tenant;
            TenantId = tenantId;
            ShoppingCart = new ShoppingCart(shoppingCartId, id, tenant);
        }

        public void Apply(CustomerDetailsChanged @event)
        {
            base.Apply(@event);

            Name = @event.Name;
            PreferredUsername = @event.PreferredUsername;
        }
        public void Apply(AddressAdded @event)
        {
            base.Apply(@event);
            var address = FindAddress(@event.Address.Id);

            if (address is null)
            {
                var _address = new Address();
                _address.CreateAddress(@event.Address);
                _address.CustomerId = Id;
                _address.Apply(@event);
                Addresses.Add(_address);
            }
        }
        public void Apply(AddressRemoved @event)
        {
            base.Apply(@event);

            var _entity = FindAddress(@event.AddressId);
            if (_entity is not null)
            {
                Addresses.Remove(_entity);
                Delete(_entity);
            }
        }

        public Address? FindAddress(Guid addressId) => Addresses.FirstOrDefault(a => a.Id == addressId);
        public ClaimsPrincipal ToClaimsPrincipal()
        {
            var claimValues = Customer.ToClaims(this);
            var claims = claimValues.Select(c => new Claim(c.Type, c.Value)).ToList();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, nameof(Customer), "name", "role"));

            return principal;
        }

        public static ICollection<ClaimValue> ToClaims(Customer customer)
        {
            var claims = new HashSet<ClaimValue>()
            {
                new ClaimValue() { Type = "preferred_username", Value = customer.PreferredUsername },
                new ClaimValue() { Type = "sub", Value = customer.Id.ToString() },
                new ClaimValue() { Type = "name", Value = customer.Name }
            };

            return claims;
        }
        public static Customer FromClaims(List<ClaimValue> claims)
        {
            if (claims.Count == 0) return null;

            var customer = new Customer(
                Guid.Parse(claims.FirstOrDefault(c => c.Type == "sub")?.Value.ToString()),
                Guid.Empty,
                claims.FirstOrDefault(c => c.Type == "first_name")?.Value ?? "",
                claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value ?? "",
                null,
                Guid.Empty);

            return customer;
        }

    }
}
