
using ESOrleansApproach.Application.Common.Interfaces;
using ESOrleansApproach.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Polly;
using Polly.Retry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace ESOrleansApproach.API.Background
{
    public class BackgroundTask : IHostedService
    {
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _provider;
        private readonly IHostEnvironment _hostEnvironment;

        public BackgroundTask(
            IConfiguration configuration,
            IServiceProvider provider,
            IHostEnvironment hostEnvironment)
        {
            _retryPolicy = Policy.Handle<Exception>()
                .WaitAndRetryAsync(10, (attempt) => TimeSpan.FromSeconds(1), (exception, timeSpan, retryCount, context) =>
                {
                    LogUtils.LogError("BackgroundError", "Error while retrying", nameof(BackgroundTask), exception.Message);
                    LogUtils.LogRetry(retryCount, 10, "BackgroundTasks is trying to perform action...");
                });

            _configuration = configuration;
            _provider = provider;
            _hostEnvironment = hostEnvironment;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}