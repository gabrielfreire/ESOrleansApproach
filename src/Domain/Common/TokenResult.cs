using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ESOrleansApproach.Domain.Common
{
    public class TokenResult
    {
        [JsonProperty("token")]
        public string Token { get; set; }
        [JsonProperty("expiryDate")]
        public DateTime ExpiryDate { get; set; }
    }
}
