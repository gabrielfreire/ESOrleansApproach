using ESOrleansApproach.Domain.Common;
using ESOrleansApproach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Polly.Retry;
using Serilog;
using ESOrleansApproach.Application.Common.Interfaces;
using ESOrleansApproach.API.Common;
using Polly;

namespace ESOrleansApproach.API.Extensions
{
    public static class StartupExtensions
    {
        private static AsyncRetryPolicy retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(10, (attempt) => TimeSpan.FromSeconds(3), (exception, timeSpan, retryCount, context) =>
            {
                LogUtils.LogError("Program", "Error while retrying", nameof(Program), exception.Message);
                LogUtils.LogRetry(retryCount, 10, "Program is trying to perform action...");
            });
        public static async Task<WebApplication> EnsureDatabaseCreated(this WebApplication app)
        {
            using (var _scope = app.Services.CreateScope())
            {
                var _configuration = _scope.ServiceProvider.GetService<IConfiguration>();
                var _connectionStringBuilder = _scope.ServiceProvider.GetRequiredService<IConnectionStringBuilder>();

                if (_configuration == null)
                {
                    throw new InvalidOperationException("Configuration not found");
                }

                try
                {
                    var isInMemory = _configuration.GetValue<bool>("UseInMemoryDatabase");

                    string _connectionString = string.Empty;

                    await retryPolicy.ExecuteAsync(() => Task.Run(async () =>
                    {
                        if (!isInMemory)
                        {
                            if (app.Environment.IsDevelopment())
                            {
                                _connectionString = _connectionStringBuilder.GetTestConnectionString();
                            }
                            else
                            {
                                _connectionString = _connectionStringBuilder.GetConnectionString();
                            }

                            using var context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                            var count = pendingMigrations.Count();
                            if (count > 0)
                            {
                                LogUtils.LogWarning("DBMigration", $"{count} database migrations will be applied", nameof(StartupExtensions));
                                await context.Database.MigrateAsync();
                            }

                            context.Database.EnsureCreated();
                            LogUtils.LogWarning("DB", $"Database creation ensured @ '{_connectionString}'", nameof(StartupExtensions));
                            LogUtils.LogWarning("DBMigration", $"{count} database migrations were applied", nameof(StartupExtensions));
                        }
                        else
                            LogUtils.LogWarning("DBMigration", $"Using in memory database", nameof(StartupExtensions));
                    }));

                    if (!isInMemory)
                    {
                        var _clusteringDbname = _configuration["OrleansConfiguration:SiloClustering:ClusteringDatabaseName"];
                        var _databaseUser = _connectionStringBuilder.GetUser(app.Environment.IsDevelopment());
                        if (string.IsNullOrEmpty(_databaseUser))
                        {
                            throw new InvalidOperationException("Database user not found");
                        }
                        if (string.IsNullOrEmpty(_clusteringDbname))
                        {
                            throw new InvalidOperationException("Clustering database name not");
                        }

                        var _clusteringConnStr = _connectionStringBuilder.GetConnectionString(_clusteringDbname);

                        /* check Clustering tables */
                        await retryPolicy.ExecuteAsync(() => Task.Run(() => OrleansClusteringInitializer.EnsureOrleansStorageCreated(
                            _clusteringDbname,
                            _connectionString,
                            _clusteringConnStr,
                            _databaseUser)));

                        /* check Reminders tables */
                        await retryPolicy.ExecuteAsync(() => Task.Run(() => OrleansRemindersPostgreStorage.EnsureRemindersTablesExist(
                            _clusteringConnStr)));

                        /* check Storage tables */
                        await retryPolicy.ExecuteAsync(() => Task.Run(() => OrleansGrainPostgreStorage.EnsureGrainStorageTablesExist(
                            _clusteringConnStr)));
                    }

                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "An error occurred while migrating the database.");
                }
            }

            return app;
        }

        public static IApplicationBuilder UseSwaggerUiWithProxy(this IApplicationBuilder builder, IConfiguration configuration)
        {
            builder.UseSwagger(c =>
            {
                c.PreSerializeFilters.Add((swagger, request) =>
                {
                    string pathBase = request.Headers["X-Forwarded-PathBase"].FirstOrDefault();
                    swagger.Servers = new List<OpenApiServer> { new OpenApiServer { Url = $"{pathBase}" } };
                });
                c.RouteTemplate = "swagger/{documentName}/swagger.json";

            });

            var _clientId = configuration.GetSection("IdentityConfiguration:ClientId").Value;
            var _clientSecret = configuration.GetSection("IdentityConfiguration:ClientSecret").Value;
            builder.UseSwaggerUI(c =>
            {
                c.DocumentTitle = "ESOrleansApproach API";
                c.SwaggerEndpoint($"/swagger/v1/swagger.json", "ESOrleansApproach API");
                if (!string.IsNullOrEmpty(_clientId))
                {
                    c.OAuthClientId(_clientId);
                }
                if (!string.IsNullOrEmpty(_clientSecret))
                {
                    c.OAuthClientSecret(_clientSecret);
                }
                c.OAuthAppName("ESOrleansApproach Server");
                c.OAuthScopeSeparator(" ");
                c.OAuthUsePkce();
            });

            return builder;
        }
    }
}
