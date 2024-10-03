using ESOrleansApproach.Application.Common.Interfaces;
using ESOrleansApproach.Domain.Common;
using ESOrleansApproach.Grains;
using ESOrleansApproach.Infrastructure.Persistence;
using ESOrleansApproach.Infrastructure.Services;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans;
using Serilog;
using Orleans.Serialization;

namespace ESOrleansApproach.API.Extensions
{
    public static class ConfigureOrleansExtensions
    {
        public static IHostBuilder ConfigureOrleans(this IHostBuilder hostBuilder)
        {
            hostBuilder.UseOrleans((ctx, builder) =>
            {
                var _isKubernetesHosted = false;
                var _podIp = Environment.GetEnvironmentVariable("POD_IP");
                var _podName = Environment.GetEnvironmentVariable("POD_NAME");
                var _podNamespace = Environment.GetEnvironmentVariable("POD_NAMESPACE");

                if (!string.IsNullOrEmpty(_podIp) &&
                    !string.IsNullOrEmpty(_podName) &&
                    !string.IsNullOrEmpty(_podNamespace))
                {
                    var _sb = LogUtils.BuildMessage("Kubernetes", "Pod", new List<Tuple<string, string>>()
                        {
                            Tuple.Create("Name", _podName),
                            Tuple.Create("IP", _podIp),
                            Tuple.Create("Namespace", _podNamespace),
                        }, null, null, MessageDirection.None);

                    Log.Information(_sb.ToString());

                    _isKubernetesHosted = true;
                }

                // MS Orleans Silo host
                // See: https://dotnet.github.io/orleans/Documentation/index.html
                // TODO: Learn more about configuring a Silo
                if (_isKubernetesHosted)
                    builder.UseKubernetesHosting();

                if (ctx.HostingEnvironment.IsDevelopment() || ctx.Configuration.GetValue<bool>("UseInMemoryDatabase"))
                {
                    builder
                        .UseLocalhostClustering()
                        .UseInMemoryReminderService()
                        .AddMemoryGrainStorage("esorleansapproach");
                } 
                else
                {
                    var _invariant = "Npgsql";
                    var clusteringDbName = ctx.Configuration["OrleansConfiguration:ClusteringDatabaseName"];
                    var host = ctx.Configuration["Database:Host"];
                    var port = ctx.Configuration["Database:Port"];
                    var user = ctx.Configuration["Database:User"];
                    var password = ctx.Configuration["Database:Password"];

                    var _clusteringConnStr = $"Server={host};Port={port};Database={clusteringDbName};User ID={user};Password='{password}';CommandTimeout=120;MaxPoolSize=600;Pooling=True";

                    builder
                        .UseAdoNetClustering(options =>
                        {
                            options.Invariant = _invariant;
                            options.ConnectionString = _clusteringConnStr;
                        })
                        .AddAdoNetGrainStorage("esorleansapproach", options =>
                        {
                            options.Invariant = _invariant;
                            options.ConnectionString = _clusteringConnStr;
                        })
                        .UseAdoNetReminderService(options =>
                        {
                            options.ConnectionString = _clusteringConnStr;
                            options.Invariant = _invariant;
                        });

                }

                var _clusteringSiloPort = ctx.Configuration.GetSection("OrleansConfiguration:SiloPort").Value;
                var _clusteringGatewatPort = ctx.Configuration.GetSection("OrleansConfiguration:GatewayPort").Value;
                var _clusteringClusterId = ctx.Configuration.GetSection("OrleansConfiguration:ClusterId").Value;
                var _clusteringServiceId = ctx.Configuration.GetSection("OrleansConfiguration:ServiceId").Value;

                builder
                    .AddLogStorageBasedLogConsistencyProvider("LogStorage")
                    .AddCustomStorageBasedLogConsistencyProvider("CustomStorage")
                    .ConfigureLogging(builder => builder.SetMinimumLevel(LogLevel.Warning).AddConsole())
                    .ConfigureEndpoints(
                            siloPort: int.Parse(_clusteringSiloPort),
                            gatewayPort: int.Parse(_clusteringGatewatPort),
                            listenOnAnyHostAddress: true)
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = _clusteringClusterId;
                        options.ServiceId = _clusteringServiceId;
                    })
                    .ConfigureServices((services) =>
                    {
                        services.AddHttpClient();
                        services.AddSingleton<ITenantService, TenantService>();
                        
                    }).Services.AddSerializer(b =>
                    {
                        b.AddNewtonsoftJsonSerializer(
                            isSupported: type => type.Namespace.StartsWith("ESOrleansApproach") || type.Namespace.StartsWith("Newtonsoft.Json"));
                    });
            });

            return hostBuilder;
        }
    }
}
