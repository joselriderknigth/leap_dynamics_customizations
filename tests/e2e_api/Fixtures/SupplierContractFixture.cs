using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Fixtures
{
    public class SupplierContractFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly SupplierContractService _supplierContracts;
        private readonly List<Guid> _createdIds = new();

        public SupplierContractFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _supplierContracts = new SupplierContractService(client);
        }

        public async Task<Guid> SetupAsync(SupplierContractConfig cfg)
        {
            var payload = new Dictionary<string, object>
            {
                ["statuscode"] = cfg.StatusCode,
                ["leap_name"] = cfg.Name,
                ["leap_Supplier@odata.bind"] = $"/accounts({cfg.SupplierId})",
                ["leap_Entity@odata.bind"] = $"/accounts({cfg.EntityId})",
                ["leap_effectivestartdate"] = cfg.EffectiveStartDate.ToString("yyyy-MM-dd"),
                ["ownerid@odata.bind"] = $"/systemusers({cfg.OwnerId})",
            };

            if (cfg.EffectiveEndDate.HasValue)
                payload["leap_effectiveenddate"] = cfg.EffectiveEndDate.Value.ToString(
                    "yyyy-MM-dd"
                );

            if (cfg.CommissionPayeeId.HasValue)
                payload["leap_CommissionPayee@odata.bind"] =
                    $"/accounts({cfg.CommissionPayeeId.Value})";

            if (cfg.AllPhysicians.HasValue)
                payload["leap_allphysicians"] = cfg.AllPhysicians.Value;

            if (cfg.AllHospitals.HasValue)
                payload["leap_allhospitals"] = cfg.AllHospitals.Value;

            var id = await _supplierContracts.CreateAsync(payload);
            _createdIds.Add(id);
            return id;
        }

        public async Task CleanupAsync()
        {
            foreach (var id in _createdIds)
                await _supplierContracts.DeleteAsync(id);

            _createdIds.Clear();
        }
    }
}
