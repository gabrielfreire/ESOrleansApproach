
using ESOrleansApproach.Domain.Common;
using ESOrleansApproach.Domain.Extensions;
using ESOrleansApproach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Orleans.EventSourcing.CustomStorage;
using Orleans.Runtime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace ESOrleansApproach.Grains
{
    public class AggregateRootBase<TState, TDbContext> : JournaledGrain<TState, EventBase>,
        ICustomStorageInterface<TState, EventBase>
            where TState : StateBase, new()
            where TDbContext : DbContext
    {
        private static readonly ActivitySource ActivitySource = new("ESOrleansApproach.EventSourcing");
        private static readonly ConcurrentDictionary<Type, System.Reflection.MethodInfo> ApplyMethodCache = new();

        private readonly IDbContextFactory<TDbContext> _dbContextFactory;
        private TState StateBeforeEvents;
        private Guid? _cachedPrimaryKey;

        public Guid GrainPrimaryKey => _cachedPrimaryKey ??= this.GetPrimaryKey();

        internal string _aggregateName;

        public AggregateRootBase(string aggregateName)
        {
            _aggregateName = aggregateName;
            _dbContextFactory = ServiceProvider.GetRequiredService<IDbContextFactory<TDbContext>>();
        }

        public Task<TState> GetManagedState()
        {
            State.Version = Version;
            return Task.FromResult(State);
        }

        protected override void OnTentativeStateChanged()
        {
            base.OnTentativeStateChanged();
        }

        protected override void OnStateChanged()
        {
            base.OnStateChanged();
            State.Version = Version;
        }

        protected override void TransitionState(TState state, EventBase @event)
        {
        }

        public async Task<List<DomainEvent>> SearchDomainEvents(ICollection<Tuple<string, object>> query = default)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var queryable = dbContext.Set<DomainEvent>()
                .AsNoTracking()
                .Where(t => t.TenantId == GrainPrimaryKey)
                .OrderByDescending(i => i.Version)
                .GetQueryable(query);

            return await queryable.ToListAsync();
        }

        public async Task<int> CountDomainEvents(ICollection<Tuple<string, object>> query = default)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var queryable = dbContext.Set<DomainEvent>()
                .AsNoTracking()
                .Where(t => t.TenantId == GrainPrimaryKey)
                .GetQueryable(query);

            return await queryable.CountAsync();
        }

        public async Task<KeyValuePair<int, TState>> ReadStateFromStorage()
        {
            using var activity = StartActivity($"{_aggregateName}.ReadStateFromStorage", BuildAggregateTags());
            using var dbContext = _dbContextFactory.CreateDbContext();

            try
            {
                var (version, state) = await ReadSnapshot(dbContext);
                state ??= new TState();

                var newVersion = await ApplyNewerEvents(dbContext, version, state);
                if (newVersion != version)
                    await WriteNewSnapshot(dbContext, newVersion, state);

                GraphSubscriber.UnsubscribeGraph(state);
                GraphSubscriber.SubscribeGraph(state);

                activity?.SetStatus(ActivityStatusCode.Ok);
                return new KeyValuePair<int, TState>(newVersion, state);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public virtual async Task<(int Version, TState state)> ReadSnapshot(TDbContext dbContext, bool includeAll = true, bool track = false)
        {
            using var activity = StartActivity($"{_aggregateName}.ReadSnapshot", BuildAggregateTags());

            var queryable = includeAll ? dbContext.Set<TState>().IncludeAll() : dbContext.Set<TState>();
            var state = track
                ? await queryable.FirstOrDefaultAsync(s => s.Id == GrainPrimaryKey)
                : await queryable.AsNoTracking().FirstOrDefaultAsync(s => s.Id == GrainPrimaryKey);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return (state?.Version ?? -2, state);
        }

        private async Task<int> ApplyNewerEvents(TDbContext dbContext, int snapshotVersion, TState state)
        {
            var newerEvents = await dbContext.Set<DomainEvent>().AsNoTracking()
                .Where(de => de.TenantId == GrainPrimaryKey && de.Version > snapshotVersion)
                .OrderBy(de => de.Version)
                .ToListAsync();

            using var activity = StartActivity(
                $"{_aggregateName}.ApplyNewerEvents",
                [
                    .. BuildAggregateTags(),
                    new("event.count", newerEvents.Count),
                    new("event.expected_version", snapshotVersion)
                ]);

            var version = snapshotVersion;

            foreach (var domainEvent in newerEvents)
            {
                version = domainEvent.Version;

                try
                {
                    var @event = DomainEvent.Deserialize(domainEvent.EventType, domainEvent.Data.Value);
                    InvokeApply(state, @event);
                    LogUtils.LogEvent(GetType().Name, $"Applied newer event {@event.GetType().Name} to version {version}");
                }
                catch (Exception ex)
                {
                    LogUtils.LogError(GetType().Name, ex.Message, GetType().Name, ex.ToString());
                    await OnExceptionThrown(ex);
                }
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            return version;
        }

        public async Task<TState> BuildStateFromEvents(TDbContext dbContext)
        {
            var events = await dbContext.Set<DomainEvent>().AsNoTracking()
                .Where(de => de.TenantId == GrainPrimaryKey)
                .OrderBy(de => de.Version)
                .ToListAsync();

            var state = new TState();

            foreach (var domainEvent in events)
            {
                var @event = DomainEvent.Deserialize(domainEvent.EventType, domainEvent.Data.Value);
                InvokeApply(state, @event);
            }

            GraphSubscriber.UnsubscribeGraph(state);
            GraphSubscriber.SubscribeGraph(state);
            return state;
        }

        private async Task WriteNewSnapshot(TDbContext dbContext, int version, TState state, List<DomainEvent>? savedDomainEvents = null)
        {
            using var activity = StartActivity(
                $"{_aggregateName}.WriteNewSnapshot",
                [
                    .. BuildAggregateTags(),
                    new("event.version", version),
                    new("event.count", savedDomainEvents?.Count ?? 0)
                ]);

            try
            {
                TState databaseAggregate;
                List<DomainEventChange> domainEventChanges = [];

                if (StateBeforeEvents is not null && StateBeforeEvents.Id != Guid.Empty)
                {
                    databaseAggregate = StateBeforeEvents;
                }
                else
                {
                    (_, databaseAggregate) = await ReadSnapshot(dbContext, true, false);
                }

                if (databaseAggregate is null)
                {
                    dbContext.Set<TState>().Add(state);
                }
                else
                {
                    DbContextUtils.TrackChanges(databaseAggregate, state, dbContext, ref domainEventChanges);
                }

                if (savedDomainEvents is { Count: > 0 } && domainEventChanges.Count > 0)
                {
                    foreach (var savedDomainEvent in savedDomainEvents)
                    {
                        savedDomainEvent.Data.Changes.AddRange(domainEventChanges);
                    }
                }

                await dbContext.SaveChangesAsync(CancellationToken.None);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                LogUtils.LogError(GetType().Name, ex.Message, GetType().Name, ex.ToString());
                await OnExceptionThrown(ex);
                throw;
            }
            catch (Exception ex)
            {
                LogUtils.LogError(GetType().Name, ex.Message, GetType().Name, ex.ToString());
                await OnExceptionThrown(ex);
            }
        }

        public virtual Task OnExceptionThrown(Exception ex)
        {
            return Task.CompletedTask;
        }

        public async Task<bool> ApplyUpdatesToStorage(IReadOnlyList<EventBase> updates, int expectedVersion)
        {
            using var activity = StartActivity(
                $"{_aggregateName}.ApplyUpdatesToStorage",
                [
                    .. BuildAggregateTags(),
                    new("event.count", updates.Count),
                    new("event.expected_version", expectedVersion)
                ]);
            using var dbContext = _dbContextFactory.CreateDbContext();
            var version = await GetEventStreamVersion(dbContext);

            if (version != expectedVersion)
            {
                LogUtils.LogEvent(GetType().Name, $"Version mismatch, expected {expectedVersion} but got {version}");
                return false;
            }

            try
            {
                List<DomainEvent> savedDomainEvents = [];

                foreach (var e in updates)
                {
                    version++;
                    e.Version = version;
                    var eventWasApplied = false;

                    try
                    {
                        InvokeApply(State, e);
                        eventWasApplied = true;
                    }
                    catch (Exception ex)
                    {
                        LogUtils.LogError(GetType().Name, ex.Message, GetType().Name, ex.ToString());
                        await OnExceptionThrown(ex);
                    }
                    finally
                    {
                        if (eventWasApplied)
                        {
                            savedDomainEvents.Add(await WriteEvent(dbContext, e));
                        }
                    }
                }

                if (State.Id != Guid.Empty)
                {
                    await WriteNewSnapshot(dbContext, version, State, savedDomainEvents);
                }

                GraphSubscriber.UnsubscribeGraph(State);
                GraphSubscriber.SubscribeGraph(State);
            }
            catch (Exception ex)
            {
                LogUtils.LogError(GetType().Name, ex.Message, GetType().Name, ex.ToString());
                await OnExceptionThrown(ex);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            return true;
        }

        private async Task<int> GetEventStreamVersion(TDbContext dbContext)
        {
            var maxVersion = await dbContext.Set<DomainEvent>().AsNoTracking()
                .Where(de => de.TenantId == GrainPrimaryKey)
                .OrderByDescending(de => de.Version)
                .Select(de => (int?)de.Version)
                .FirstOrDefaultAsync();
            return maxVersion ?? -2;
        }

        private async Task<DomainEvent> WriteEvent(TDbContext dbContext, EventBase @event, bool saveDbContextChanges = false)
        {
            var domainEvent = new DomainEvent(GrainPrimaryKey, @event.Version, @event);
            var httpContext = RequestContext.Get(nameof(HttpContextSurrogate)) as HttpContextSurrogate;

            domainEvent.ApplyRequestContext(httpContext);
            domainEvent.ApplyActivityContext(Activity.Current);
            domainEvent.Source ??= GetType().Name;
            domainEvent.CorrelationId ??= @event.Id.ToString();

            var entity = dbContext.Add(domainEvent);

            if (saveDbContextChanges)
                await dbContext.SaveChangesAsync();

            return entity.Entity;
        }

        public async Task<List<EventBase>> GetEvents(int? fromVersion = 0, int? toVersion = 0)
        {
            return toVersion != 0
                ? (await base.RetrieveConfirmedEvents(fromVersion!.Value, toVersion.Value)).ToList()
                : (await base.RetrieveConfirmedEvents(0, Version)).ToList();
        }

        public async Task<List<EventBase>> GetEvents(DateTimeOffset from, DateTimeOffset to)
        {
            var storedEvents = await base.RetrieveConfirmedEvents(0, Version);
            return storedEvents.Where(se => se.Timestamp >= from && se.Timestamp <= to).ToList();
        }

        public async Task RaiseEventAsync(EventBase @event, Func<TDbContext, StateBase, Task> synchronizationCallback = null)
        {
            using var activity = StartActivity($"{_aggregateName}.RaiseEvent", [.. BuildAggregateTags(), new("event.type", @event.GetType().Name)]);

            try
            {
                StateBeforeEvents = CloneState();
                base.RaiseEvent(@event);
                await base.ConfirmEvents();
                GraphSubscriber.UnsubscribeGraph(State);
                GraphSubscriber.SubscribeGraph(State);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                LogUtils.LogError(GetType().Name, ex.Message, GetType().Name, ex.ToString());
                await OnExceptionThrown(ex);
                throw;
            }
        }

        public async Task RaiseEventsAsync(List<EventBase> events, Func<TDbContext, StateBase, Task> synchronizationCallback = null)
        {
            using var activity = StartActivity($"{_aggregateName}.RaiseEvents", [.. BuildAggregateTags(), new("event.count", events.Count)]);

            try
            {
                StateBeforeEvents = CloneState();
                base.RaiseEvents(events);
                await base.ConfirmEvents();
                GraphSubscriber.UnsubscribeGraph(State);
                GraphSubscriber.SubscribeGraph(State);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                LogUtils.LogError(GetType().Name, ex.Message, GetType().Name, ex.ToString());
                await OnExceptionThrown(ex);
                throw;
            }
        }

        private static void InvokeApply(TState state, EventBase @event)
        {
            var eventType = @event.GetType();
            var apply = ApplyMethodCache.GetOrAdd(eventType, static eventRuntimeType =>
                typeof(TState).GetMethod("Apply", new[] { eventRuntimeType })
                ?? throw new InvalidOperationException($"No Apply method found on {typeof(TState).Name} for {eventRuntimeType.Name}."));

            apply.Invoke(state, [@event]);
        }

        public TState CloneState() => (TState)JsonDeepUtils.DeepClone((object)State)!;

        private Activity StartActivity(string name, IEnumerable<KeyValuePair<string, object>> tags)
        {
            var activity = ActivitySource.StartActivity(name);
            if (activity is null)
                return null;

            foreach (var tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }

            return activity;
        }

        private KeyValuePair<string, object>[] BuildAggregateTags()
        {
            return
            [
                new("aggregate.name", _aggregateName),
                new("grain.type", GetType().Name),
                new("grain.primary_key", GrainPrimaryKey)
            ];
        }
    }
}
