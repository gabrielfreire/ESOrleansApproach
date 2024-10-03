using ESOrleansApproach.Application.Common.Interfaces;
using ESOrleansApproach.Domain.Common;
using MediatR;
using MediatR.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ESOrleansApproach.Application.Common.Behaviours
{
    public class RequestLogger<TRequest> : IRequestPreProcessor<TRequest>
    {
        private readonly ITenantService _tenantService;
        public RequestLogger(ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        public async Task Process(TRequest request, CancellationToken cancellationToken)
        {
            var _reqName = typeof(TRequest).Name;
            var _tenant = _tenantService.GetCurrentTenant();

        }
    }
}
