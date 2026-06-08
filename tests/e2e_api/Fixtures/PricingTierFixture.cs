using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Fixtures
{
    public class PricingTierFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly PricingTierService _pricingTiers;
        private readonly List<Guid> _createdIds = new();

        public PricingTierFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _pricingTiers = new PricingTierService(client);
        }

        public async Task<Guid> SetupAsync(PricingTierConfig cfg)
        {
            var payload = new Dictionary<string, object>
            {
                ["statecode"] = _config.DefaultStateCode,
                ["statuscode"] = cfg.StatusCode,
                ["ownerid@odata.bind"] = $"/systemusers({cfg.OwnerId})",
                ["leap_SupplierContractId@odata.bind"] = $"/leap_suppliercontracts({cfg.SupplierContractId})",
                ["leap_ProductId@odata.bind"] = $"/products({cfg.ProductId})",
                ["leap_pricelowerbound"] = cfg.PriceLowerBound,
                ["leap_priceupperbound"] = cfg.PriceUpperBound,
            };

            if (cfg.Name is not null)
                payload["leap_name"] = cfg.Name;

            if (cfg.CommissionRate.HasValue)
                payload["leap_leapcommissionrate"] = cfg.CommissionRate.Value;

            var id = await _pricingTiers.CreateAsync(payload);
            _createdIds.Add(id);
            return id;
        }

        public async Task CleanupAsync()
        {
            foreach (var id in _createdIds)
                await _pricingTiers.DeleteAsync(id);

            _createdIds.Clear();
        }
    }
}
