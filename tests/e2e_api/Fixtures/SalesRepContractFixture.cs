using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Fixtures
{
    public class SalesRepContractFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly SalesRepContractService _salesRepContracts;
        private readonly List<Guid> _createdIds = new();

        public SalesRepContractFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _salesRepContracts = new SalesRepContractService(client);
        }

        public async Task<Guid> SetupAsync(SalesRepContractConfig cfg)
        {
            var payload = new Dictionary<string, object>
            {
                ["statuscode"] = cfg.StatusCode,
                ["leap_name"] = cfg.Name,
                ["leap_SalesRep@odata.bind"] = $"/systemusers({cfg.SalesRepId})",
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

            if (cfg.SalesCategory != null)
                payload["leap_salescategory"] = cfg.SalesCategory;

            if (cfg.CommissionSplit.HasValue)
                payload["leap_commissionsplit"] = cfg.CommissionSplit.Value;

            if (cfg.AllPhysicians.HasValue)
                payload["leap_allphysicians"] = cfg.AllPhysicians.Value;

            if (cfg.AllHospitals.HasValue)
                payload["leap_allhospitals"] = cfg.AllHospitals.Value;

            var id = await _salesRepContracts.CreateAsync(payload);
            _createdIds.Add(id);
            return id;
        }

        public async Task CleanupAsync()
        {
            foreach (var id in _createdIds)
                await _salesRepContracts.DeleteAsync(id);

            _createdIds.Clear();
        }
    }
}
