using ESOrleansApproach.Domain.Entities;
using ESOrleansApproach.Domain.Events;
using ESOrleansApproach.GrainInterfaces;
using ESOrleansApproach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Orleans.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Grains
{
    [LogConsistencyProvider(ProviderName = "CustomStorage")]
    [StorageProvider(ProviderName = "esorleansapproach")]
    public partial class TenantAggregate : AggregateRootBase<Tenant, ApplicationDbContext>, ITenantAggregate
    {
        private string aggrName = "TenantAggregate";
        private IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly IConfiguration _configuration;
        public TenantAggregate() : base("TenantAggregate")
        {
            _dbContextFactory = ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            _configuration = ServiceProvider.GetRequiredService<IConfiguration>();
        }
        public override async Task<(int Version, Tenant state)> ReadSnapshot(
            ApplicationDbContext dbContext, 
            bool includeAll = true)
        {
            Tenant? tenant = null;

            if (includeAll)
            {
                tenant = await dbContext.Tenants
                    .IncludeAll()
                    .AsNoTracking()
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(t => t.Id == GrainPrimaryKey);
            }
            else
            {
                tenant = await dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == GrainPrimaryKey);
            }

            if (tenant is null)
                return (-2, null);

            return (tenant?.Version ?? -2, tenant);
        }

        public Task<Tenant> GetCurrentState() => base.GetManagedState();
        public async Task<Tenant> OnboardTenant(string customerName, string customerEmail, string tenantName)
        {
            var customer = new Customer(
                Guid.NewGuid(), 
                Guid.NewGuid(), 
                customerName,
                customerEmail, 
                null, 
                Guid.Empty);

            await RaiseEventAsync(new TenantOnboarded(aggrName, this.GetPrimaryKey(), customer, tenantName));

            return await GetManagedState();
        }

    }
}
