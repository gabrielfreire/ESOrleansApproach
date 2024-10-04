 # Event-Sourcing Orleans Approach

How to use MS Orleans JournaledGrain with EF Core for a robust event-sourcing / DDD implementation

- Grain interfaces `src\GrainInterfaces\`

- JournaledGrain implementation `src\Grains\AggregateRootBase.cs`

- Entities
    - `src\Domain\Common\DomainEvent.cs`
    - `src\Domain\Entities\Tenant.cs`
    - `src\Domain\Entities\Tenant.Customers.cs`
    - `src\Domain\Entities\Tenant.ShoppingCarts.cs`
    - `src\Domain\Entities\Customer.cs`
    - `src\Domain\Entities\ShoppingCart.cs`
    - `src\Domain\Entities\ShoppingCartItem.cs`

- Aggregate implementation 
    - `src\Grains\TenantAggregate.cs`
    - `src\Grains\TenantAggregate.CustomersManagement.cs`
    - `src\Grains\TenantAggregate.ShoppingCartManagement.cs`

- DbContext is in `src\Infrastructure\Persistence\ApplicationDbContext.cs` 



## Run integration tests

Best way to checkout how this project works is to run the integration tests at `tests\Application.IntegrationTests`

The relevant test file is `TenantAggregateTests.cs`

**Note:** You need a postgresSQL database server to run this project

**Note:** Database migration already exists, database will be created and migration applied automatically when tests run

Change the configuration in 

`tests\Application.IntegrationTests\appsettings.json`

Replace values in

```json
"Database": {
  "Host": "localhost",
  "Port": "5432",
  "User": "postgre",
  "DbName": "esorleansapproach.test.db",
  "Password": "xxx"
},
"DatabaseTest": {
  "Host": "localhost",
  "Port": "5432",
  "User": "postgre",
  "DbName": "esorleansapproach.test.db",
  "Password": "xxx"
},
```

## The Why and The How

Why? Because I wanted to achieve a pure ES/DDD POCO implementation in Orleans, I really like this open-source actor framework by Microsoft, it is quite powerful and it has many great features. In this pure POCO implementation the idea is to only make POCO like changes to an entity/class by raising an event to a JournaledGrain, this would call `.Apply(EventBase @event)` in the state POCO class which would be responsible for any manipulation in one or multiple nested levels, for example, to add a customer to a tenant we would call `RaiseEvent(new CustomerAdded(...))` in the `JournaledGrain<Tenant>`, then in `Tenant` `.Apply(CustomerAdded @event)` would be triggered which would look like below:

```csharp
public void Apply(CustomerAdded @event)
{
    base.Apply(@event);
    var customer = new Customer(
        @event.CustomerId, 
        @event.Name, 
        @event.Email);
    
    Customers.Add(customer);
}
```

**Note:** Consider that this is a very simple example that only performs an `Add`, 
check the solution in this repository for more complex examples. 

Then EF Core would automatically pick that up and know how to properly mirror/apply 
these changes to the database accurately, whether it was one Add, two Add's and one 
Update, or many Deletes, Updates and Adds accross many different entities that are 
related to each other. I spent hours trying to make this work and almost gave up but finally got around to solve it. 

The main core state class is `src\Domain\Entities\Tenant.cs`, our custom event-sourcing 
log storage entity is `src\Domain\Common\DomainEvent.cs`, from there I mapped 
one-to-many/one-to-one relationships, a tenant has many customers, stores, 
files, a customer has many addresses, a shopping cart, many orders, etc. 
Relationships are configured in the `DbContext` in 
`override void OnModelCreating(ModelBuilder builder)` method.

I implemented `ReadStateFromStorage` and `ApplyUpdatesToStorage` methods in 
`AggregateRootBase.cs` class, check that out for details on the integration 
between Orleans JournaledGrain with EF Core.

All seemed ok at first glance but when running tests nothing worked, deleted entities were 
not being deleted, added entities were being tracked as modified and the 
ChangeTracker seemed to be doing some very messy work.

I encountered many many issues related to how EFCore works out of the box 
in relation to how it tracks entities. 

For example, if you look at the method `WriteNewSnapshot` in `AggregateRootBase.cs` 
class you will see that in order to update the state I simply call 
`dbContext.Set<TState>().Update(state)`, I expected that EFCore would automagically 
know exactly what to do in it's ChangeTracker based on the current state and previous 
state once I called that method, well... I was far from right, calling `.Update(state)` 
tracks all entities related to the base state entity as `Modified`, 
even the ones that were just added for the first time, this 
throws a massive ugly exception with a big cryptic message that makes it very hard to know what is really going on
, it took me hours of debugging to understand what was happening, 
also it doesn't track the ones that were deleted when using `List.Remove` in a 
collection for a one-to-many relationship for example. 
This is obviously not good for my use case and EF Core doesn't work the way I thought 
out of the box and I didn't want to have to use a reference of the DbContext class to 
manually track entities like `_dbContext.Customers.Remove(customer)` because it would 
contaminate the POCO classes, I just wanted to use my pure POCO object, raise events 
and have everything just work as expected after a `.SaveChangesAsync` call.

How? The way EF Core works out of the box is as follows: when you call `SaveChangesAsync`
after `.Update(state)`, even if some of the entities were added, deleted, modified 
or not touched at all, the ChangeTracker will always track them as `Modified` 
because of that `.Update(state)` call. So, the solution for deleted entities was to use 
a temporary list of `DeletedEntities` in `StateBase` to track deleted entities, for 
updated entities it was to update the `UpdatedOnUtc` property of an entity 
as well as manually compare database values and current values in the ChangeTracker. 
To make this happen I had to override `SaveChangesAsync` in `ApplicationDbContext.cs`. 
We first delete all entities marked for deletion in `DeletedEntities` list, 
check if they were not already deleted, mark all of then as `Deleted` and then clear the list, 
then we check if the current entity is marked as `Modified`, if yes, we check if 
it already exists on the database by calling `.GetDatabaseValues()` in the ChangeTracker 
and mark it as `Added` if not, if they existed before it means that they 
could've been modified so I use the values from the database and compare `.CurrentValues` 
to check for changes, if there were no changes I mark the entity as `Detached` 
otherwise I leave it as `Modified`. That's it! This seems to work flawlessly, I have 
written many unit/integration tests trying to break it but all my tests pass so and the 
application works as expected, I am hopeful this is a good approach overall.

This solution allowed me to create very complex data relationships from a single state object,
in this case the `Tenant.cs` class is the highest level and make any changes just 
by raising events and making pure POCO modifications, I don't ever need to use a reference of 
the `DbContext` because everything is magically handled by the `AggregateRootBase.cs` JournaledGrain and
the EF Core ChangeTracker.

Also, using a single main state class and build all the data relationships from there is 
just more intuitive to me, that's why I used this approach, I understand this is not the 
mainstream way of doing things but I really like it.
If you think of an e-commerce multi-tenanted software platform, you have multiple tenants, a tenant
is your highest level, then each tenant
has many customers, each customer has many roles, a shopping cart and many orders, each tenant
has many stores, each store has many pages, products, categories, themes, etc. This means that
you could end up with a massive `TenantAggregate.cs` god file with thousands of lines of code which
happened to me but I solved this issue by using `partial class`'s, that's why you will find things like
`TenantAggregate.cs`, `TenantAggregate.CustomersManagement.cs`, `TenantAggregate.ShoppingCartManagement.cs`,
`Tenant.cs`, `Tenant.Customers.cs`, `Tenant.ShoppingCarts.cs`, etc, in this solution. This is completely
acceptable to me but may not be for other people, most engineers seem to prefer a more obvious separation.  

This way of doing things makes it possible to represent the entire software solution by 
just looking at the state class and using Orleans allows us to keep the entire data 
for tenants in memory which makes queries extremelly efficient.

```csharp
[GenerateSerializer]
public partial class Tenant : StateBase
{
    [Id(0)]
    public string Name { get; private set; }
    // ....
    [Id(4)]
    public List<Customer> Customers { get; private set; } = [];
    [Id(5)]
    public List<Store> Stores { get; private set; } = [];
    // ....
    [Id(7)]
    public List<Role> Roles { get; private set; } = [];
    [Id(8)]
    public List<OnlineCustomer> OnlineCustomers { get; private set; } = [];
    [Id(9)]
    public List<Log> Logs { get; private set; } = [];
    // ...
    [Id(11)]
    public TenantSubscription ActiveSubscription { get; private set; }
}
[GenerateSerializer]
public class Customer : StateBase
{
    [Id(0)]
    public string Name { get; private set; }
    [Id(1)]
    private List<Guid> _roleIds = [];
    public List<Guid> RoleIds { get => _roleIds; }
    [Id(2)]
    public List<Address> Addresses { get; private set; } = [];
    // .....
    [Id(3)]
    public List<Order> Orders { get; private set; } = [];
    [Id(4)]
    public ShoppingCart ShoppingCart { get; private set; }
    [Id(5)]
    public Guid TenantId { get; private set; }
}

[GenerateSerializer]
public class Address : StateBase
{
    [Id(0)]
    public string FirstName { get; private set; }
    // ...
    [Id(6)]
    public string City { get; private set; }
    // ...
    [Id(13)]
    public Guid CustomerId { get; private set; }
}
```


Although this solution seems to work I am not sure this approach is the best. 
The reason for this is because the `SaveChangesAsync` looks weird to me and not very 
intuitive at all, maybe someone has done it this way before, maybe this is a 
crazy way to do things, maybe I am overcomplicating, I don't know, I'm not a EF Core expert, 
I am open to suggestions.


## ApplicationDbContext.cs -> SaveChangesAsync override

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
{
    
    var (username, tenant) = GetCurrentTenant();
    
    foreach (var entry in ChangeTracker.Entries<StateBase>())
    {
        // delete entities marked for deletion
        if (entry.Entity.DeletedEntities.Any())
        {
            foreach (var deletedEntity in entry.Entity.DeletedEntities)
            {
                var existingEntity = GetExistingEntity(((StateBase)deletedEntity).Id, deletedEntity.GetType());
                if (existingEntity is null)
                    entry.Context.Entry(deletedEntity).State = EntityState.Detached;
                else
                    entry.Context.Entry(existingEntity).State = EntityState.Deleted;
                
            }

            entry.Entity.DeletedEntities.Clear();
        }

        // check an entity marked as modified was actually modified
        // if not, Add or Detach it
        if (entry.State == EntityState.Modified)
        {
            var originalValues = entry.GetDatabaseValues();

            if (originalValues is null)
                entry.State = EntityState.Added;
            else
            {

                bool valuesHaveChanged = false;

                foreach (var prop in entry.Properties)
                {
                    var currentValue = entry.Property(prop.Metadata.Name).CurrentValue;
                    var originalValue = originalValues.GetValue<object>(prop.Metadata.Name);

                    if (currentValue?.ToString() != originalValue?.ToString())
                    {
                        valuesHaveChanged = true;
                        break;
                    }
                }

                if (!valuesHaveChanged)
                    entry.State = EntityState.Detached;
                
            }
        }
        switch (entry.State)
        {
            case EntityState.Added:
                entry.Entity.CreatedOnUtc = DateTimeOffset.UtcNow;
                entry.Entity.Created = true;

                if (!string.IsNullOrWhiteSpace(username))
                    entry.Entity.CreatedBy = username;

                if (!string.IsNullOrEmpty(tenant))
                    entry.Entity.Tenant = tenant;
                break;
            case EntityState.Modified:
                entry.Entity.UpdatedOnUtc = DateTimeOffset.UtcNow;

                if (!string.IsNullOrWhiteSpace(username))
                    entry.Entity.UpdatedBy = username;

                if (!string.IsNullOrEmpty(tenant))
                    entry.Entity.Tenant = tenant;
                break;
        }
    }

    var res = await base.SaveChangesAsync(cancellationToken);

    ChangeTracker.Clear();`

    return res;
}
```
