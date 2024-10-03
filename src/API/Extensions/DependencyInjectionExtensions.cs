using ESOrleansApproach.API.Background;
using ESOrleansApproach.API.Filters;
using ESOrleansApproach.Application;
using ESOrleansApproach.Application.Common.Interfaces;
using FluentValidation.AspNetCore;
using ESOrleansApproach.Infrastructure;
using ESOrleansApproach.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace ESOrleansApproach.API.Extensions
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {

            services.AddControllers();

            services.AddApplication();
            services.AddInfrastructure(configuration, environment);

            services.AddHttpContextAccessor();

            services.AddCors();

            services.AddHealthChecks();
            services.Configure<KestrelServerOptions>(options =>
            {
                // Set max request header size (for Kestrel server)
                options.Limits.MaxRequestHeadersTotalSize = 1048576; // 1 MB, adjust as needed
            });
            // API controllers & filters
            services.AddSingleton<ValidateTenantFilterAttribute>();
            services.AddControllers()
                .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<ApplicationDbContext>())
                .AddNewtonsoftJson();

            // Customise default API behaviour
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Contact = new OpenApiContact
                    {
                        Name = "User name",
                        Email = "user.name@imsmaxims.com",
                        Url = new Uri("https://localhost:5003")
                    },
                    Description = "ESOrleansApproach API is part of the SMART Platform distributed system",
                    Title = "ESOrleansApproach API",
                    Version = "1.0"
                });

                /* uncomment if you wish to use only bearer token */
                //c.AddSecurityDefinition("JWT", new OpenApiSecurityScheme
                //{
                //    Type = SecuritySchemeType.ApiKey,
                //    Name = "Authorization",
                //    In = ParameterLocation.Header,
                //    Description = "Type into the textbox: Bearer {your JWT token}."
                //});

                var _authority = configuration.GetSection("IdentityConfiguration:Authority").Value;

                c.OperationFilter<AuthorizeSwaggerOperationFilter>();
                c.DescribeAllParametersInCamelCase();
                
                if (!string.IsNullOrEmpty(_authority))
                {
                    c.AddSecurityDefinition("OAuth2", new OpenApiSecurityScheme
                    {
                        Name = "OAuth2",
                        Type = SecuritySchemeType.OAuth2,
                        Flows = new OpenApiOAuthFlows()
                        {
                            AuthorizationCode = new OpenApiOAuthFlow
                            {
                                AuthorizationUrl = new Uri($"{_authority}/protocol/openid-connect/auth"),
                                TokenUrl = new Uri($"{_authority}/protocol/openid-connect/token")
                            }
                        },
                        Description = "ESOrleansApproach Security Gateway"

                    });
                }

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);

            });


            services.AddSingleton<BackgroundTask>();
            services.AddSingleton<IHostedService>(sp => sp.GetService<BackgroundTask>());

            return services;
        }
    }
}
