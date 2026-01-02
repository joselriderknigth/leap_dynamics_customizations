using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Fixtures
{
    public class CaseProductFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly CaseProductService _caseProducts;
        private readonly List<Guid> _createdIds = new();

        public CaseProductFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _caseProducts = new CaseProductService(client);
        }

        // TODO: accept CaseProductConfig object that has inner CaseProductFlags and pricing/qty fields for configuration
        // also calculate extended price maybe?
        public async Task<Guid> SetupAsync(CaseProductConfig cfg)
        {
            var payload = new Dictionary<string, object>
            {
                ["statecode"] = _config.DefaultStateCode,
                ["ownerid@odata.bind"] = $"/systemusers({_config.OwnerUserId})",
                ["leap_unitprice"] = cfg.UnitPrice,
                ["leap_quantity"] = cfg.Quantity,
                ["leap_Supplier@odata.bind"] = $"/accounts({cfg.SupplierId})",
                ["leap_ProductId@odata.bind"] = $"/products({cfg.ProductId})",
                ["leap_Hospital@odata.bind"] = $"/accounts({cfg.HospitalId})",
                ["leap_CaseId@odata.bind"] = $"/leap_cases({cfg.CaseId})",
                ["leap_caseextendedprice"] = cfg.Quantity * cfg.UnitPrice,
                ["leap_name"] = _config.CorrelationId,
                ["leap_producttype"] = cfg.ProductType,
            };

            if (cfg.IsHospitalOwned)
                payload["leap_hospitalowned"] = true;

            if (cfg.IsIO)
            {
                payload["leap_io"] = true;
                payload["leap_ioquantity"] = cfg.IOQuantity;
                payload["leap_iounitprice"] = cfg.IOUnitPrice;
                payload["leap_ioextendedprice"] = cfg.IOQuantity * cfg.IOUnitPrice;
            }

            if (cfg.IsRestock)
                payload["leap_restock"] = true;

            var id = await _caseProducts.CreateAsync(payload);
            _createdIds.Add(id);
            return id;
        }

        public async Task CleanupAsync()
        {
            foreach (var id in _createdIds)
                await _caseProducts.DeleteAsync(id);

            _createdIds.Clear();
        }
    }
}
