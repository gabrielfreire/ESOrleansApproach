using Microsoft.AspNetCore.Authentication;
using Microsoft.Net.Http.Headers;
using ESOrleansApproach.Domain.Common;

namespace ESOrleansApproach.API.Common
{
    public class AuthenticationContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IGrainFactory _grainFactory;
        public AuthenticationContextMiddleware(RequestDelegate next, IGrainFactory grainFactory)
        {
            _next = next;
            _grainFactory = grainFactory;
        }

        /// <summary>
        /// Set a few properties into context for Orleans to access
        /// and figure out customer and store access
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext != null)
            {
                var _httpContextSurr = (HttpContextSurrogate)RequestContext.Get(nameof(HttpContextSurrogate));

                if (_httpContextSurr is null)
                {
                    _httpContextSurr = new HttpContextSurrogate();
                }

                // are we authenticated?
                var token = await httpContext.GetTokenAsync("access_token");
                if (!string.IsNullOrWhiteSpace(token) &&
                    httpContext.User is not null &&
                    httpContext.User.Identity is not null)
                {
                    var claims = httpContext.User.Claims;
                    var claimsValueList = claims.Select(c =>
                                        new ClaimValue()
                                        {
                                            Type = c.Type,
                                            Value = c.Value
                                        }).ToList();


                    _httpContextSurr.UserClaims = httpContext.User.Identity.IsAuthenticated ? claimsValueList : new HashSet<ClaimValue>();
                    _httpContextSurr.Host = httpContext.Request.Headers[HeaderNames.Host];
                    _httpContextSurr.IpAddress = GetIPAddress(httpContext);
                    _httpContextSurr.RequestPath = httpContext.Request.Path;
                    _httpContextSurr.FullAccessToken = token;
                }
                else
                {
                    _httpContextSurr.Host = httpContext.Request.Headers[HeaderNames.Host];
                    _httpContextSurr.IpAddress = GetIPAddress(httpContext);
                    _httpContextSurr.RequestPath = httpContext.Request.Path;
                }

                await HandleLogout(_httpContextSurr);

                RequestContext.Set(nameof(HttpContextSurrogate), _httpContextSurr);
            }

            await _next(httpContext);
        }

        /// <summary>
        /// Notify the visit tracker that the user has logged out
        /// </summary>
        /// <param name="httpContextSurrogate"></param>
        /// <returns></returns>
        private Task HandleLogout(HttpContextSurrogate httpContextSurrogate)
        {
            // do nothing
            return Task.CompletedTask;
        }

        protected string GetIPAddress(HttpContext context)
        {
            string? ipAddress = context.Request.Headers["X-Forwarded-For"];

            if (!string.IsNullOrEmpty(ipAddress))
            {
                string[] addresses = ipAddress.Split(',');
                if (addresses.Length != 0)
                {
                    return addresses[0];
                }
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "No IP";
        }
    }

    public static class AuthenticationContextMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuthenticationRequestContext(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthenticationContextMiddleware>();
        }
    }
}
