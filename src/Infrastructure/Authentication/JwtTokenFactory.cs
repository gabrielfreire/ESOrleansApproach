using ESOrleansApproach.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ESOrleansApproach.Infrastructure.Authentication
{
    public static class JwtTokenFactory
    {
        public static JwtSecurityToken FromHttpContext(HttpContext context)
        {
            string authHeader = context.Request.Headers["Authorization"];
            
            if (authHeader == null) return null;

            var token = authHeader.Replace("Bearer ", "");

            var tokenHandler = new JwtSecurityTokenHandler();

            return tokenHandler.ReadJwtToken(token);
        }

        public static JwtSecurityToken FromHttpContext(HttpContextSurrogate context)
        {
            var token = context.FullAccessToken;

            if (string.IsNullOrEmpty(token))
                return null;

            var tokenHandler = new JwtSecurityTokenHandler();

            return tokenHandler.ReadJwtToken(token);
        }
    }
}
