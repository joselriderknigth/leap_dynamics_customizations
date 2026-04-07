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
                ["statecode"] = cfg.StatusCode,
                ["leap_contracttype"] = cfg.ContractType,
                ["leap_maxcommissionrate"] = cfg.MaxCommissionRate,
                ["leap_mincommissionrate"] = cfg.MinCommissionRate,
                ["leap_startdate"] = cfg.StartDate.ToString("yyyy-MM-dd"),
                ["ownerid@odata.bind"] = $"/systemusers({cfg.OwnerId})",
            };

            if (cfg.ContractNumber != null)
                payload["leap_contractnumber"] = cfg.ContractNumber;

            if (cfg.Name != null)
                payload["leap_name"] = cfg.Name;

            if (cfg.RepRole.HasValue)
                payload["leap_reprole"] = cfg.RepRole.Value;

            if (cfg.Salaried.HasValue)
                payload["leap_salaried"] = cfg.Salaried.Value;

            if (cfg.EndDate.HasValue)
                payload["leap_enddate"] = cfg.EndDate.Value.ToString("yyyy-MM-dd");

            if (cfg.ExternalRepId.HasValue)
                payload["leap_ContactId@odata.bind"] = $"/contacts({cfg.ExternalRepId.Value})";

            if (cfg.DistributorId.HasValue)
                payload["leap_DistributorId@odata.bind"] = $"/accounts({cfg.DistributorId.Value})";

            if (cfg.ExternalAgentId.HasValue)
                payload["leap_ExternalAgent@odata.bind"] = $"/systemusers({cfg.ExternalAgentId.Value})";

            if (cfg.SalesRepId.HasValue)
                payload["leap_SalesRepId@odata.bind"] = $"/systemusers({cfg.SalesRepId.Value})";

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
