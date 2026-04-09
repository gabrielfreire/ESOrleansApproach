using Orleans;
using System.Collections.Generic;

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
        [Id(3)]
        public string WebStoreContextId { get; set; }
        [Id(4)]
        public string RequestPath { get; set; }
        [Id(5)]
        public string FullAccessToken { get; set; }
        [Id(6)]
        public string Username { get; set; }
        [Id(7)]
        public string Tenant { get; set; }
        [Id(8)]
        public string UserAgent { get; set; }
    }
}
