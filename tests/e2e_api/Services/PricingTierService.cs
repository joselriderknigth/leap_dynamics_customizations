using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class PricingTierService : DynamicsServiceBase<PricingTierService>
    {
        protected override string EntityName => EntityNames.PricingTier;

        public PricingTierService(DataverseClient client)
            : base(client) { }
     
    }
}
