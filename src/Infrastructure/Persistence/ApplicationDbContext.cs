using ESOrleansApproach.Application.Common.Interfaces;
using ESOrleansApproach.Domain.Common;
using ESOrleansApproach.Domain.Entities;
using ESOrleansApproach.Domain.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Npgsql;
using Orleans.Runtime;
using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ESOrleansApproach.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        private object _tenant;

        private ITenantService _tenantService;
        private IServiceProvider _serviceProvider;
        private IConnectionStringBuilder _connectionStringBuilder;

        public ApplicationDbContext(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _connectionStringBuilder = _serviceProvider.GetRequiredService<IConnectionStringBuilder>();
            _tenantService = _serviceProvider.GetRequiredService<ITenantService>();
            _tenant = _tenantService.GetCurrentTenant();
        }

        public DbSet<DomainEvent> DomainEvents { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<ShoppingCartItem> ShoppingCartItems { get; set; }
        public DbSet<ShoppingCart> ShoppingCarts { get; set; }
        private (string username, string tenant) GetCurrentTenant()
        {

            var httpContext = (HttpContextSurrogate)RequestContext.Get("HttpContextSurr");
            if (httpContext is not null)
            {
                if (httpContext.UserClaims is not null && httpContext.UserClaims.Any())
                {
                    var currentCustomer = Customer.FromClaims(
                        httpContext.UserClaims.ToList());
                    return (currentCustomer.PreferredUsername, currentCustomer.Tenant);
                }
            }
            return (null, null);
        }
        /// <summary>
        /// Update Auditable properties
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            var verboseTracker = false;
            var onlyChangeTrackerDebugView = false;

            var (username, tenant) = GetCurrentTenant();

            if (verboseTracker || onlyChangeTrackerDebugView)
                LogUtils.LogEvent("ChangeTracker", "Before ChangeTracker Updates\n" + ChangeTracker.DebugView.ShortView);

            foreach (var entry in ChangeTracker.Entries<StateBase>())
            {
                // delete entities marked for deletion
                if (entry.Entity.DeletedEntities.Any())
                {
                    foreach (var deletedEntity in entry.Entity.DeletedEntities)
                    {
                        var existingEntity = GetExistingEntity(((StateBase)deletedEntity).Id, deletedEntity.GetType());
                        if (existingEntity is null)
                        {
                            if (verboseTracker)
                                LogUtils.LogEvent("ChangeTracker", $"Entity {entry.Entity.GetType().Name}: {entry.Entity.Id} was already deleted");
                            entry.Context.Entry(deletedEntity).State = EntityState.Detached;
                        }
                        else
                        {

                            entry.Context.Entry(existingEntity).State = EntityState.Deleted;
                            if (verboseTracker)
                                LogUtils.LogEvent("ChangeTracker", $"Entity {entry.Entity.GetType().Name}: {entry.Entity.Id} will be deleted");
                        }
                    }

                    entry.Entity.DeletedEntities.Clear();
                }

                // check an entity marked as modified is actually modified
                // if not, detach it
                if (entry.State == EntityState.Modified && !entry.Entity.Deleted)
                {

                    //var existingEntity = GetExistingEntity(entry.Entity.Id, entry.Entity.GetType());
                    var originalValues = entry.GetDatabaseValues();

                    if (originalValues is null)
                    {
                        entry.State = EntityState.Added;
                        if (verboseTracker)
                            LogUtils.LogEvent("ChangeTracker", $"Entity {entry.Entity.GetType().Name}: {entry.Entity.Id} was tracked as Modified but it doesn't exist so it will be Added");
                        Debug.WriteLine($"Entity {entry.Entity.GetType().Name}: {entry.Entity.Id} was tracked as Modified but it doesn't exist so it will be Added");
                    }
                    else
                    {

                        bool valuesHaveChanged = false;

                        if (verboseTracker) LogUtils.LogEvent("ChangeTracker", $"Checking {entry.Entity.GetType().Name}!");

                        foreach (var prop in entry.Properties)
                        {
                            var currentValue = entry.Property(prop.Metadata.Name).CurrentValue;
                            var originalValue = originalValues.GetValue<object>(prop.Metadata.Name);

                            if (currentValue?.ToString() != originalValue?.ToString())
                            {
                                if (verboseTracker) LogUtils.LogEvent(
                                    "ChangeTracker",
                                    $"Values differ -> \n\rOld Value ({prop.Metadata.Name} = {originalValue?.ToString()}) | \n\rNew Value ({prop.Metadata.Name} = {currentValue?.ToString()})");

                                valuesHaveChanged = true;
                                break;
                            }
                        }

                        if (!valuesHaveChanged)
                        {
                            if (verboseTracker)
                                LogUtils.LogEvent("ChangeTracker", $"Entity {entry.Entity.GetType().Name} was tracked as modified but didn't change");

                            entry.State = EntityState.Detached;
                        }
                        else
                        {
                            if (verboseTracker)
                                LogUtils.LogEvent("ChangeTracker", $"Entity {entry.Entity.GetType().Name} was correctly tracked as modified because valeus changed");
                        }
                    }
                }
                Debug.WriteLine($"{entry.Entity.GetType().Name} - {entry.State}");

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
            if (verboseTracker || onlyChangeTrackerDebugView)
                LogUtils.LogEvent("ChangeTracker", "After ChangeTracker Updates\n" + ChangeTracker.DebugView.ShortView);
            Debug.WriteLine("\n" + ChangeTracker.DebugView.ShortView);

            var res = await base.SaveChangesAsync(cancellationToken);
            ChangeTracker.Clear();
            return res;
        }
        private StateBase? GetExistingEntity(Guid entityId, Type entityType)
        {
            // here we must use reflection to fetch a record from the Database in order to set
            // it's values to the state. Our database must always be in sync with MS Orleans
            // but the database ends up being the main soruce of truth instead of the Orleans Log Consistency Storage
            // because the Log Consistency Storage keeps deleted items which renders it impossible to track them with EF.
            // The issue is that the deleted item will always be marked for deletion even when it is not in the database.
            // By fetching the record from EF and setting it to the current state we ensure the deleted item is actually deleted
            var parameter = Expression.Parameter(entityType, "e");
            // Create expressions for the properties you want to select
            var bindings = new MemberBinding[]
            {
                CreateMemberBinding(parameter, nameof(StateBase.Id)),
                CreateMemberBinding(parameter, nameof(StateBase.CreatedOnUtc)),
                CreateMemberBinding(parameter, nameof(StateBase.UpdatedOnUtc))
            };
            // Create the body of the select clause using NewExpression
            var body = Expression.MemberInit(Expression.New(entityType), bindings);
            // Build the lambda expression for the select clause
            var selector = Expression.Lambda(body, parameter);

            // create DbContext.Set method
            var _setMethod = this.GetType().GetMethods().Where(x => x.Name == nameof(DbContext.Set))
            .FirstOrDefault(x => x.IsGenericMethod);

            // create IQueryable<T> instance
            var _iQueryable = _setMethod.MakeGenericMethod(entityType).Invoke(this, null);

            // create query methods
            // AsNoTracking(), FirstOrDefault()
            var _asNoTrackingMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .First(x => x.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTracking)).MakeGenericMethod(entityType);
            var _firstOrDefaultMethod = typeof(Queryable).GetMethods()
                .First(x => x.Name == nameof(Queryable.FirstOrDefault) && x.GetParameters().Length == 2 && x.GetParameters()[1].ParameterType.IsAssignableTo(typeof(Expression)))
                .MakeGenericMethod(entityType);
            var _selectMethod = typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
                .MakeGenericMethod(entityType, entityType);
            // Create the filter expression e => e.Id == 123123-123123-123123-123

            var property = Expression.Property(parameter, "Id");
            var filterPropertyExpr = Expression.Property(parameter, "Id");
            var filterValueExpr = Expression.Constant(entityId);
            var equalsExpr = Expression.Equal(filterPropertyExpr, filterValueExpr);
            var filterExpression = Expression.Lambda(equalsExpr, parameter);

            try
            {
                // get record by id without tracking
                _iQueryable = _asNoTrackingMethod.Invoke(null, new object[] { _iQueryable });
                _iQueryable = _selectMethod.Invoke(null, new object[] { _iQueryable, selector });
                var _dbItemForState = _firstOrDefaultMethod.Invoke(null, new object[] { _iQueryable, filterExpression });

                if (_dbItemForState is not null)
                {
                    return _dbItemForState as StateBase;

                }
            }
            catch (Exception ex)
            {
                LogUtils.LogError("GetExistingEntity", ex.Message, this.GetType().Name, ex.ToString());
            }
            return null;
        }
        private MemberBinding CreateMemberBinding(ParameterExpression parameter, string propertyName)
        {
            var property = Expression.Property(parameter, propertyName);
            var member = typeof(StateBase).GetProperty(propertyName);
            return Expression.Bind(member, property);
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Tenant>().Property(t => t.Id).ValueGeneratedNever();
            builder.Entity<Customer>().Property(t => t.Id).ValueGeneratedNever();
            builder.Entity<Address>().Property(t => t.Id).ValueGeneratedNever();
            builder.Entity<ShoppingCart>().Property(t => t.Id).ValueGeneratedNever();

            builder.Entity<Tenant>()
                .HasMany(s => s.Customers).WithOne()
                .HasForeignKey(t => t.TenantId);

            builder.Entity<Customer>().HasOne(s => s.ShoppingCart).WithOne()
                .HasForeignKey<ShoppingCart>(t => t.CustomerId);

            builder.Entity<Customer>()
                .HasMany(s => s.Addresses).WithOne()
                .HasForeignKey(c => c.CustomerId);

            builder.Entity<ShoppingCart>()
                .HasMany(s => s.Items).WithOne()
                .HasForeignKey(c => c.ShoppingCartId);

            builder.Entity<DomainEvent>()
                .Property(b => b.Data)
                .Metadata.SetValueComparer(new ValueComparer<EventData>(
                    (a, b) => JsonConvert.SerializeObject(a) == JsonConvert.SerializeObject(b),
                    (v) => v == null ? 0 : JsonConvert.SerializeObject(v).GetHashCode(),
                    (v) => JsonConvert.DeserializeObject<EventData>(JsonConvert.SerializeObject(v))
                ));

            // apply entity configurations in ./Configurations/ before creating model
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // pre-filter queries by tenant
            // builder.Entity<Event>().HasQueryFilter(b => EF.Property<string>(b, "Tenant") == _tenant.Tenant.Name);

            #region JSONB data EF tracker
            // use the code below if you want EF to track JSONB columns on postgresql
            //builder.Entity<Entity>().Property(b => b.Data).Metadata.SetValueConverter(new ValueConverter<DataEntity, string>(
            //        v => JsonConvert.SerializeObject(v),
            //        v => JsonConvert.DeserializeObject<DataEntity>(v) ?? new DataEntity()
            //    ));

            //builder.Entity<Entity>().Property(b => b.Data).Metadata.SetValueComparer(
            //    new ValueComparer<DataEntity>(
            //        (a, b) => JsonConvert.SerializeObject(a) == JsonConvert.SerializeObject(b),
            //        (v) => v == null ? 0 : JsonConvert.SerializeObject(v).GetHashCode(),
            //        (v) => JsonConvert.DeserializeObject<DataEntity>(JsonConvert.SerializeObject(v))
            //    ));
            #endregion

            base.OnModelCreating(builder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var isDevelopment = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "development", StringComparison.InvariantCultureIgnoreCase);

            var _conStr = isDevelopment ?
                _connectionStringBuilder.GetTestConnectionString() :
                _connectionStringBuilder.GetConnectionString();

            optionsBuilder.UseNpgsql(_conStr, b => b.MigrationsAssembly(this.GetType().Assembly.FullName))
                .EnableDetailedErrors(true)
                .EnableSensitiveDataLogging(true);

            NpgsqlConnection.GlobalTypeMapper.UseJsonNet();
        }
    }
}
