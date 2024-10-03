using ESOrleansApproach.Application.Common.Interfaces;
using ESOrleansApproach.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ESOrleansApproach.Application.Common.Behaviours
{
    public class RequestPerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        private readonly Stopwatch _timer;
        private readonly ITenantService _tenantService;

        public RequestPerformanceBehavior(ITenantService tenantService)
        {
            _timer = new Stopwatch();
            _tenantService = tenantService;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _timer.Start();
            var response = await next();
            _timer.Stop();

            var _elapsed = _timer.ElapsedMilliseconds;
            var _requestName = typeof(TRequest).Name;
            var _tenant = _tenantService.GetCurrentTenant();


            return response;
        }
    }
}
