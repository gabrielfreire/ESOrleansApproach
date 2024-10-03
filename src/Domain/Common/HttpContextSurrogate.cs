using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Common
{
    [GenerateSerializer]
    public class HttpContextSurrogate
    {
        [Id(0)]
        public string Host { get; set; }
        [Id(1)]
        public string IpAddress { get; set; }
        [Id(2)]
        public ICollection<ClaimValue> UserClaims { get; set; } = new List<ClaimValue>();
        [Id(4)]
        public string RequestPath { get; set; }
        [Id(5)]
        public string FullAccessToken { get; set; }
        
    }
}
