using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Common
{
    [GenerateSerializer]
    public record ClaimValue
    {
        [Id(0)]
        public string Type { get; set; }
        [Id(1)]
        public string Value { get; set; }
    }
}
