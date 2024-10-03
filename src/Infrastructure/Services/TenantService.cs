using ESOrleansApproach.Application.Common.Interfaces;
using ESOrleansApproach.Domain.Common;
using ESOrleansApproach.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;
using Serilog;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans.Runtime;

namespace ESOrleansApproach.Infrastructure.Services
{
    public class TenantService : ITenantService
    {
        public JwtSecurityToken AccessToken { get; set; }

        public TenantService()
        {
        }

        // replace object with your User class
        public object GetCurrentTenant()
        {
            var httpContext = (HttpContextSurrogate)RequestContext.Get(nameof(HttpContextSurrogate));
            if (httpContext != null)
            {
                // TODO build user from claims
                return new { };
            }

            return null;
        }
    }
}
