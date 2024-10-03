using Microsoft.Extensions.Configuration;
using System;
using System.Runtime.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ESOrleansApproach.Application.Common.Interfaces;
using ESOrleansApproach.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using ESOrleansApproach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ESOrleansApproach.Infrastructure.Authentication;
using System.Net.Http;
using Microsoft.Extensions.Hosting;

namespace ESOrleansApproach.Infrastructure
{
    public static class DependencyInjection
    {
        /// <summary>
        /// Main
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment hostEnvironment)
        {
            services.AddAuthentication(configuration);
            services.AddAuthorization();
            services.AddDbContextFactory<ApplicationDbContext>();
            // multi-tenancy services
            services.AddTransient<IConnectionStringBuilder, ConnectionStringBuilder>();
            services.AddSingleton<ITenantService, TenantService>();

            return services;
        }

        /// <summary>
        /// Authentication
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var authority = configuration["IdentityConfiguration:Authority"];

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuerSigningKey = false,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidIssuer = authority
                };
                options.RequireHttpsMetadata = false;
            });

            return services;
        }



    }
}
