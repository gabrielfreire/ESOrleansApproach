using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Extensions
{
    public static class TenantQueryExtensions
    {
        public static List<Customer> SearchCustomers(this Tenant tenant, ICollection<Tuple<string, object>> queries = null)
        {
            var queryable = tenant.Customers
                .AsQueryable();

            return queryable.GetQueryable(queries)
                .OrderByDescending(o => o.UpdatedOnUtc)
                .OrderByDescending(o => o.CreatedOnUtc)
                .ToList();
        }
        public static int CountCustomers(this Tenant tenant, ICollection<Tuple<string, object>> queries = null)
        {
            var queryable = tenant.Customers
                .AsQueryable();

            return queryable.GetQueryable(queries)
                .Count();
        }
    }
}
