using ESOrleansApproach.API.Extensions;
using ESOrleansApproach.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using Orleans.TestingHost;
using System.Diagnostics;

namespace Application.IntegrationTests
{
    [SetUpFixture]
    public class Testing
    {

        public static IServiceScopeFactory _scopeFactory;
        public static IServiceScopeFactory _orleansScopeFactory;

        private static IConfiguration _configuration;
        private static IWebHostEnvironment hostedEnvironment;

        public static TestCluster _cluster;

        private static ServiceCollection services;

        [OneTimeSetUp]
        public async Task RunBeforeAnyTests()
        {

            // add configuration from appsettings.json
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            hostedEnvironment = Mock.Of<IWebHostEnvironment>(w =>
                                    w.ApplicationName == "API" &&
                                    w.EnvironmentName == "Development");

            // wire up services to use while testing
            services = new ServiceCollection();
            services.AddScoped<IConfiguration>(sp => _configuration);
            services.AddSingleton<IHostEnvironment>(hostedEnvironment);
            services.AddApplicationServices(_configuration, hostedEnvironment);

            await AddTestSiloAndInjectGrainFactory();

            // get a scope factory
            _scopeFactory = services.BuildServiceProvider().GetService<IServiceScopeFactory>();

            await EnsureDatabaseCreated();

        }

        [OneTimeTearDown]
        public async Task Finish()
        {
            using var scope = _scopeFactory.CreateScope();

            var _dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();

            _dbContext.Database.EnsureDeleted();

            if (_cluster == null) return;
            try
            {
                await _cluster.StopAllSilosAsync().ConfigureAwait(false);
            }
            finally
            {
                _cluster.Dispose();
            }
        }

        public static async Task EnsureDatabaseCreated()
        {
            try
            {

                using var scope = _scopeFactory.CreateScope();

                var _dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();

                _dbContext.Database.EnsureDeleted();
                _dbContext.Database.EnsureCreated();

                var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync();
                var count = pendingMigrations.Count();
                if (count > 0)
                {
                    Debug.WriteLine($"{count} database migrations will be applied");
                    await _dbContext.Database.MigrateAsync();
                }
                await _dbContext.Database.EnsureCreatedAsync();

            }
            catch (Exception ex) { }
        }

        public static async Task AddTestSiloAndInjectGrainFactory()
        {
            var clusterBuilder = new TestClusterBuilder();
            clusterBuilder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
            _cluster = clusterBuilder.Build();
            await _cluster.DeployAsync();
        }

        public static async Task<TEntity> FindAsync<TEntity>(string id) where TEntity : class
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FindAsync<TEntity>(id);
        }
        public static async Task AddAsync<TEntity>(TEntity entity) where TEntity : class
        {
            using var scope = _scopeFactory.CreateScope();
            
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            context.Add(entity);

            await context.SaveChangesAsync();
        }
        
        public static async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)     
        {
            using var scope = _scopeFactory.CreateScope();

            var _mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            return await _mediator.Send(request);

        }
        public static async Task SendAsync(MediatR.IRequest request)     
        {
            using var scope = _scopeFactory.CreateScope();

            var _mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await _mediator.Send(request);

        }


        private class TestSiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder hostBuilder)
            {
                // we use a slowed-down memory storage provider
                hostBuilder
                    .AddCustomStorageBasedLogConsistencyProvider("CustomStorage")
                    .AddLogStorageBasedLogConsistencyProvider("LogStorage")
                    .AddMemoryGrainStorage("esorleansapproach")
                    .ConfigureServices((services) =>
                    {
                        services.AddHttpClient();
                        var sp = services.BuildServiceProvider();

                        services.AddScoped<IConfiguration>(sp => _configuration);

                        services.AddSingleton<IHostEnvironment>(hostedEnvironment);
                        services.AddApplicationServices(_configuration, hostedEnvironment);

                        _orleansScopeFactory = services.BuildServiceProvider().GetService<IServiceScopeFactory>();
                    });
            }
        }

    }
}