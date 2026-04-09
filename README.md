# Event-Sourcing Orleans Approach

This repository shows one way to combine Orleans `JournaledGrain`, EF Core, and a POCO-style domain model to build an event-sourced aggregate that can still persist a relational snapshot of a deep object graph.

It is intentionally a small explanation repository, not a full production template. The goal is to make the approach understandable: raise events against an Orleans aggregate, mutate plain state objects in `Apply(...)` methods, replay events when needed, and let EF Core persist the resulting graph without forcing the domain model to depend directly on `DbContext` commands.

## Table of Contents

- [Relevant Files](#relevant-files)
- [Tests](#tests)
- [The Why and The How](#the-why-and-the-how)
- [How Snapshot Persistence Works](#how-snapshot-persistence-works)
- [What This Repository Includes](#what-this-repository-includes)
- [Tradeoffs](#tradeoffs)

## Relevant Files

Core event-sourcing infrastructure:

- `src/Grains/AggregateRootBase.cs`
- `src/Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/Infrastructure/Persistence/DbContextUtils.cs`
- `src/Domain/Common/StateBase.cs`
- `src/Domain/Common/EventBase.cs`
- `src/Domain/Common/DomainEvent.cs`
- `src/Domain/Common/DomainEventChange.cs`
- `src/Domain/Common/GraphSubscriber.cs`
- `src/Domain/Common/JsonDeepUtils.cs`
- `src/Domain/Common/HttpContextSurrogate.cs`

Sample aggregate and state graph:

- `src/Grains/TenantAggregate.cs`
- `src/Grains/TenantAggregate.CustomersManagement.cs`
- `src/Grains/TenantAggregate.ShoppingCartManagement.cs`
- `src/Domain/Entities/Tenant.cs`
- `src/Domain/Entities/Tenant.Customers.cs`
- `src/Domain/Entities/Tenant.ShoppingCarts.cs`
- `src/Domain/Entities/Customer.cs`
- `src/Domain/Entities/Address.cs`
- `src/Domain/Entities/ShoppingCart.cs`
- `src/Domain/Entities/ShoppingCartItem.cs`

Tests:

- `tests/Application.IntegrationTests/TenantAggregateTests.cs`

## Tests

The best way to understand the project is to read the integration tests in `tests/Application.IntegrationTests`.

This repository still expects PostgreSQL for the application-level integration path. Update `tests/Application.IntegrationTests/appsettings.json` before running those tests.

Example settings:

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
}
```

## The Why and The How

The motivation for this approach is simple: keep the domain model expressive and event-driven without leaking EF Core commands into every entity.

In this repository the aggregate root is an Orleans `JournaledGrain<TState, EventBase>`. Commands do not directly manipulate tables. Instead, a command raises an event, Orleans confirms that event through custom storage, and the state graph changes through `Apply(...)` methods on plain C# objects.

That means the domain code stays close to the business model. For example, adding a customer is represented as an event and the `Tenant` state reacts to that event by mutating its in-memory graph:

```csharp
public void Apply(CustomerAdded @event)
{
    if (!Customers.Any(c => c.Id == @event.CustomerId))
    {
        var customer = new Customer(
            @event.CustomerId,
            @event.ShoppingCartId,
            @event.Name,
            @event.PreferredUsername,
            Name,
            Id);

        Subscribe(customer);
        Customers.Add(customer);
        base.Apply(@event);
    }
}
```

The important part is not just that the collection changes. The state graph is also wired through change notifications. `StateBase` exposes a `Changed` event, child entities can be subscribed to their parent, and `GraphSubscriber` can rebuild the subscription graph after replay or snapshot loading. That prevents deep mutations from becoming invisible to the aggregate root.

This repository is built around a single top-level aggregate state, `Tenant`, which owns a deeper object graph: customers, addresses, shopping carts, and cart items. That is not the only valid way to design an event-sourced system, but it is a useful way to demonstrate how Orleans and EF Core can cooperate when the domain model is intentionally rich and nested.

## How Snapshot Persistence Works

The difficult part is not replaying events. The difficult part is making EF Core persist a changed aggregate graph correctly.

Calling `dbContext.Update(state)` on a complex graph is usually too blunt for this scenario. EF Core will often mark too much as modified, fail to distinguish adds from updates, or miss deletions when objects disappear from nested collections. That is the gap this repository is trying to address.

The current implementation uses a three-step persistence flow:

1. `AggregateRootBase` clones the state before events are confirmed.
2. Events are applied to the in-memory graph.
3. `DbContextUtils.TrackChanges(...)` compares the cloned snapshot with the new state and tells EF Core exactly which entities and properties should be added, updated, or deleted.

The event log entity, `DomainEvent`, also stores a JSON payload and a list of `DomainEventChange` entries. That gives the sample repository a lightweight audit trail of what changed when the event was persisted.

At a high level, the storage lifecycle is:

```text
Raise event
-> Orleans confirms event through custom storage
-> event is written to DomainEvents
-> state snapshot is rebuilt or updated
-> cloned pre-event state is diffed against the current state
-> EF Core persists only the real graph changes
```

This is why `AggregateRootBase.ReadStateFromStorage`, `AggregateRootBase.ApplyUpdatesToStorage`, and `ApplicationDbContext.SaveChangesAsync` are the most important parts of the sample.

## What This Repository Includes

This repository now includes a lean version of the more advanced event-sourcing core used in the reference implementation:

- Cached `Apply(...)` method resolution in `AggregateRootBase`
- `StateBeforeEvents` snapshot cloning before confirmation
- Deep clone helpers in `JsonDeepUtils`
- Graph re-subscription in `GraphSubscriber`
- Request and trace metadata on `DomainEvent`
- `DomainEventChange` capture during snapshot persistence
- Optimistic concurrency checks on the event stream version

It does not try to bring over every production concern from a larger system. For example, this repository keeps the focus on storage, replay, and graph diffing instead of pulling in webhook dispatching or a larger observability package.

## Tradeoffs

This approach is powerful, but it is not free.

- The aggregate root becomes a meaningful integration point between Orleans, EF Core, and the domain model.
- Snapshot persistence is more sophisticated than a standard CRUD repository.
- Deep graphs are convenient to model, but they require disciplined `Apply(...)` methods and reliable change propagation.
- The EF Core integration is intentionally explicit because the default graph tracking behavior is not precise enough for this use case.

That said, the upside is substantial when the model fits: the aggregate can stay expressive, events remain the source of behavioral change, Orleans keeps the aggregate in memory, and EF Core still gives you a relational projection of the current state.

If you are evaluating this repository, start with `TenantAggregate`, then read `AggregateRootBase`, `StateBase`, and `DbContextUtils` in that order. That path makes the design much easier to follow.
