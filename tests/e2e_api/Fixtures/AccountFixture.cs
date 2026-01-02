using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Fixtures
{
    public class AccountFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly AccountService _accounts;
        private readonly List<Guid> _createdIds = new();

        public AccountFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _accounts = new AccountService(client);
        }

        public async Task<Guid> SetupHospitalAsync()
        {
            var hospitalPayload = new Dictionary<string, object>
            {
                ["name"] = $"Test Hospital {_config.CorrelationId}",
                ["ownerid@odata.bind"] = $"/systemusers({_config.OwnerUserId})",
                ["statecode"] = _config.DefaultStateCode,
                ["statuscode"] = _config.DefaultStatusCode,
            };
            var accountId = await _accounts.CreateAsync(hospitalPayload);
            _createdIds.Add(accountId);
            return accountId;
        }

        public async Task<Guid> SetupSupplierAsync()
        {
            var supplierPayload = new Dictionary<string, object>
            {
                ["name"] = $"Test Supplier {_config.CorrelationId}",
                ["ownerid@odata.bind"] = $"/systemusers({_config.OwnerUserId})",
                ["statecode"] = 0,
            };

            var id = await _accounts.CreateAsync(supplierPayload);
            _createdIds.Add(id);
            return id;
        }

        public async Task CleanupAsync()
        {
            foreach (var id in _createdIds)
                await _accounts.DeleteAsync(id);

            _createdIds.Clear();
        }
    }
}
