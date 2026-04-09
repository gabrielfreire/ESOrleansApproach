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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ESOrleansApproach.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IConnectionStringBuilder _connectionStringBuilder;

        public ApplicationDbContext(IServiceProvider serviceProvider)
        {
            _connectionStringBuilder = serviceProvider.GetRequiredService<IConnectionStringBuilder>();
        }

        public DbSet<DomainEvent> DomainEvents { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<ShoppingCartItem> ShoppingCartItems { get; set; }
        public DbSet<ShoppingCart> ShoppingCarts { get; set; }
        private (string username, string tenant) GetCurrentTenant()
        {
            var httpContext = RequestContext.Get(nameof(HttpContextSurrogate)) as HttpContextSurrogate;
            if (httpContext is not null)
            {
                if (!string.IsNullOrWhiteSpace(httpContext.Username) || !string.IsNullOrWhiteSpace(httpContext.Tenant))
                    return (httpContext.Username, httpContext.Tenant);

                if (httpContext.UserClaims is not null && httpContext.UserClaims.Any())
                {
                    var currentCustomer = Customer.FromClaims(
                        httpContext.UserClaims.ToList());
                    if (currentCustomer is not null)
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
            var (username, tenant) = GetCurrentTenant();

            foreach (var entry in ChangeTracker.Entries<StateBase>())
            {
                entry.Entity.DeletedEntities.Clear();

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
            ChangeTracker.Clear();
            return res;
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Tenant>().Property(t => t.Id).ValueGeneratedNever();
            builder.Entity<Customer>().Property(t => t.Id).ValueGeneratedNever();
            builder.Entity<Address>().Property(t => t.Id).ValueGeneratedNever();
            builder.Entity<ShoppingCart>().Property(t => t.Id).ValueGeneratedNever();
            builder.Entity<ShoppingCartItem>().Property(t => t.Id).ValueGeneratedNever();

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
