


namespace ESOrleansApproach.Grains
{
    public class AggregateRootBase<TState, TDbContext> : JournaledGrain<TState, EventBase>,
        ICustomStorageInterface<TState, EventBase>
            where TState : StateBase, new()
            where TDbContext : DbContext
    {
        //private readonly TDbContext _dbContext;

        private readonly IDbContextFactory<TDbContext> _dbContextFactory;

        private bool WriteSnapshotBusy = false;
        public Guid GrainPrimaryKey { get => this.GetPrimaryKey(); }
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

        #region Query events
        public async Task<List<DomainEvent>> SearchDomainEvents(ICollection<Tuple<string, object>> query = default)
        {
            using var _dbContext = _dbContextFactory.CreateDbContext();

            var queryable = _dbContext.Set<DomainEvent>()
                .AsNoTracking()
                .Where(t => t.TenantId == GrainPrimaryKey)
                .OrderByDescending(i => i.Version)
                .GetQueryable(query);

            return await queryable.ToListAsync();
        }
        public async Task<int> CountDomainEvents(ICollection<Tuple<string, object>> query = default)
        {
            using var _dbContext = _dbContextFactory.CreateDbContext();

            var queryable = _dbContext.Set<DomainEvent>()
                .AsNoTracking()
                .Where(t => t.TenantId == GrainPrimaryKey)
                .OrderByDescending(i => i.Version)
                .GetQueryable(query);

            return await queryable.CountAsync();
        }
        #endregion

        #region overrides and log storage management

        protected override void OnStateChanged()
        {
            base.OnStateChanged();
            State.Version = Version;
        }
        protected override async void TransitionState(TState state, EventBase @event) { }

        public async Task<KeyValuePair<int, TState>> ReadStateFromStorage()
        {

            using var dbContext = _dbContextFactory.CreateDbContext();

            var (version, state) = await ReadSnapshot(dbContext);

            if (state is null)
                state = (TState)Activator.CreateInstance(typeof(TState), new object[] { });

            var newVersion = await ApplyNewerEvents(dbContext, version, state);

            if (newVersion != version) await WriteNewSnapshot(dbContext, newVersion, state);

            version = newVersion;
            return new KeyValuePair<int, TState>(version, state);
        }

        public virtual async Task<(int Version, TState state)> ReadSnapshot(TDbContext dbContext, bool includeAll = true)
        {
            var queryable = includeAll ? dbContext.Set<TState>().IncludeAll(): dbContext.Set<TState>();

            var state = await queryable.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == GrainPrimaryKey);

            return (state?.Version ?? -2, state);
        }
        private async Task<int> ApplyNewerEvents(TDbContext dbContext, int snapshotVersion, TState state)
        {
            var newerEvents = await dbContext.Set<DomainEvent>().AsNoTracking()
                .Where(de => de.TenantId == GrainPrimaryKey && de.Version > snapshotVersion)
                .OrderBy(de => de.Version).ToListAsync();

            var version = snapshotVersion;

            foreach (var de in newerEvents)
            {
                version = de.Version;

                try
                {

                    var @event = DomainEvent.Deserialize(de.EventType, de.Data.Value);
                    var apply = typeof(TState).GetMethod("Apply", new[] { @event.GetType() });
                    apply.Invoke(state, new object[] { @event });

                    Debug.WriteLine($"Applied newer event to version {snapshotVersion} {@event.GetType().Name}. Version is now {version}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    await OnExceptionThrown(ex);
                }
            }

            return version;
        }
        private async Task WriteNewSnapshot(TDbContext dbContext, int version, TState state)
        {
            try
            {
                var (_, _databaseAggregate) = await ReadSnapshot(dbContext, false);

                if (_databaseAggregate is null)
                {
                    dbContext.Set<TState>().Add(state);
                }
                else
                {
                    // we call update here but manage the changeTracker ourselves in DbContext
                    dbContext.Set<TState>().Update(state);
                }

                await dbContext.SaveChangesAsync(CancellationToken.None);

                Debug.WriteLine($"Saved state version {version}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                await OnExceptionThrown(ex);
            }
        }

        public virtual Task OnExceptionThrown(Exception ex)
        {
            return Task.CompletedTask;
        }

        public async Task<bool> ApplyUpdatesToStorage(IReadOnlyList<EventBase> updates, int expectedVersion)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var version = await GetEventStreamVersion(dbContext);

            if (version != expectedVersion)
            {
                LogUtils.LogEvent("ApplyUpdatesToStorage", $"Version mismatch, expected {expectedVersion} but got {version}");
                return false;
            }

            try
            {

                foreach (var e in updates)
                {
                    version++;
                    e.Version = version;
                    await WriteEvent(dbContext, e);

                    try
                    {
                        var apply = typeof(TState).GetMethod("Apply", new[] { e.GetType() });
                        apply.Invoke(State, new object[] { e });
                        Debug.WriteLine($"-> Applied {e.GetType().Name}");
                    }
                    catch (Exception ex)
                    {
                        LogUtils.LogError($"{_aggregateName}:ApplyUpdatesToStorage@Apply() Error", ex.Message, GetType().Name, ex.ToString());
                        await OnExceptionThrown(ex);
                    }

                }

                if (State.Id != Guid.Empty)
                {
                    await WriteNewSnapshot(dbContext, version, State);
                }
            }
            catch (Exception ex)
            {
                LogUtils.LogError($"{_aggregateName}:ApplyUpdatesToStorage Error", ex.Message, GetType().Name, ex.ToString());

                await OnExceptionThrown(ex);
            }

            return true;
        }
        private async Task<int> GetEventStreamVersion(TDbContext dbContext)
        {
            var greaterVersionEvent = await dbContext.Set<DomainEvent>().AsNoTracking()
                .Where(de => de.TenantId == GrainPrimaryKey)
                .Select(de => new { de.Id, de.Version })
                .OrderByDescending(de => de.Version)
                .FirstOrDefaultAsync();
            return greaterVersionEvent?.Version ?? -2;
        }

        private async Task WriteEvent(TDbContext dbContext, EventBase @event)
        {
            dbContext.Add(new DomainEvent(GrainPrimaryKey, @event.Version, @event));
            await dbContext.SaveChangesAsync();
        }

        #endregion

        #region Raise Event(s)
        /// <summary>
        /// Raise event and publish to queue passing custom MassTransit message type
        /// </summary>
        /// <param name="event">Event to be raise</param>
        /// <param name="synchronizationCallback">Synchronization callback can be used to synchronize the grain state with a different database</param>
        /// <returns></returns>
        public async Task RaiseEventAsync(EventBase @event, Func<TDbContext, StateBase, Task> synchronizationCallback = null)
        {

            try
            {

                base.RaiseEvent(@event);
                await base.ConfirmEvents();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogUtils.LogError($"{_aggregateName}:RaiseEventAsync Error", ex.Message, GetType().Name, ex.ToString());
                await OnExceptionThrown(ex);
                throw;
            }
        }
        public async Task RaiseEventsAsync(List<EventBase> @events, Func<TDbContext, StateBase, Task> synchronizationCallback = null)
        {

            try
            {

                base.RaiseEvents(@events);
                await base.ConfirmEvents();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                LogUtils.LogError($"{_aggregateName}:RaiseEventAsync Error", ex.Message, GetType().Name, ex.ToString());
                await OnExceptionThrown(ex);
                throw;
            }
        }
        #endregion



    }
}
