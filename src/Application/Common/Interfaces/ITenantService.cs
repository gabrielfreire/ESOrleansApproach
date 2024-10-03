using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Application.Common.Interfaces
{
    public interface ITenantService
    {
        JwtSecurityToken AccessToken { get; set; }
        object GetCurrentTenant();


    }
}
