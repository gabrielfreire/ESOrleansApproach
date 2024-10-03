 # Event-Sourcing Orleans Approach

How to use MS Orleans JournaledGrain with EF Core for a robust event-sourcing / DDD implementation

- Grain interfaces `src\GrainInterfaces\`

- JournaledGrain implementation `src\Grains\AggregateRootBase.cs`

- Aggregate implementation 
    - `src\Grains\TenantAggregate.cs`
    - `src\Grains\TenantAggregate.CustomersManagement.cs`
    - `src\Grains\TenantAggregate.ShoppingCartManagement.cs`

- DbContext is in `src\Infrastructure\Persistence\ApplicationDbContext.cs` 

## Method

The main core state class is `src\Domain\Entities\Tenant.cs`, from there I mapped relationships like a tenant has many customers, stores, files, a customer has many addresses, a shopping cart, many orders, etc. Relationships are configured in the `DbContext` in `override void OnModelCreating(ModelBuilder builder)` method

I wanted to achieve a pure DDD POCO implementation where I would only make POCO like changes to an entity and EFCore would automatically pick that up and know how to properly mirror any entity changes to the database accurately. I spent hours trying to make this work, almost gave up, I am not sure my approach is the best but it works flawlessly so far.

Reading up on the Orleans documentation I implemented `ReadStateFromStorage` and `ApplyUpdatesToStorage` methods in `AggregateRootBase.cs`

All seemed ok but when running tests nothing worked, deleted entities were not being deleted, added entities were being tracked as modified and the ChangeTracker seemed to be doing some very messy work.

I encountered many many issues related to how EFCore works out of the box in relation to how it tracks entities. 

For example, if you look at the method `WriteNewSnapshot` in `AggregateRootBase.cs` class you will see that in order to update the state I simply call `dbContext.Set<TState>().Update(state)`, I expected that EFCore would automagically know exactly what to do in it's ChangeTracker based on the current state and previous state once I called that method but I was far from right, calling `.Update(state)` will track all entities related to the base state entity as modified, even the ones that were just added which throws a massive ugly exception and it doesn't track the ones that were deleted when using `List.Remove` in a collection. This is not optimal and I didn't want to have to use the dbContext for anything, I just wanted to use my POCO object, raise events and have everything just work.

Some of the entities may have been added or deleted or not modified at all, but the ChangeTracker would always track them as `Modified`, the solution for deleted entities was to use a temporary list of `DeletedEntities` in `StateBase` to track deleted entities and to update the `UpdatedOnUtc` property of an entity to track updates as well as manually compare database values and current values in the ChangeTracker, to make this happen I override `SaveChangesAsync` in `ApplicationDbContext.cs`. We first delete all entities marked for deletion in `DeletedEntities` list, mark all of then as `Deleted` and then clear the list, then we check if the current entity is marked as `Modified`, if yes, we check if it already exists on the database by calling `.GetDatabaseValues()` in the ChangeTracker and mark it as `Added` if not, if they existed before it means that they could've been modified so I use the values from the database and compare `.CurrentValues` to check for changes, if there were no changes I mark the entity as `Detached` otherwise I leave it as `Modified`, this seems to work flawlessly as I have written many unit/integration tests trying to break it but all my tests pass so I am hopeful this is a good approach overall.

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