using ESOrleansApproach.Infrastructure.Authentication;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ESOrleansApproach.API.Filters
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ValidateTenantFilterAttribute : ActionFilterAttribute
    {
        private readonly IConfiguration _configuration;

        public ValidateTenantFilterAttribute(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public override Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {

            var urlPathParts = context.HttpContext.Request.Path.Value.Split("/", StringSplitOptions.RemoveEmptyEntries);

            if (urlPathParts.Length >= 2)
            {
                var tenantFromPath = urlPathParts[1];

                var _token = JwtTokenFactory.FromHttpContext(context.HttpContext);

                if (_token == null)
                    throw new UnauthorizedAccessException($"Authorization token not found");
                
                // get tenant from group claim
                var tenant = _token.Claims.FirstOrDefault(c => c.Type == _configuration.GetSection("TenantConfiguration:TenantClaimType").Value);

                if (tenant == null)
                    throw new UnauthorizedAccessException($"Claim {_configuration.GetSection("TenantConfiguration:TenantClaimType").Value} was not present in the token");

                // does current user belong in the correct tenant
                if (tenant.Value != tenantFromPath)
                    throw new UnauthorizedAccessException($"Unauthorized action from user in {tenant.Value}");

                context.HttpContext.Response.Headers.Add("X-Tenant-ID", tenant.Value);
            }
            
            return base.OnActionExecutionAsync(context, next);
        }
    }
}
