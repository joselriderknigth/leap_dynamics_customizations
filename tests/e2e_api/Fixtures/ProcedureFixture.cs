using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;
using E2E.Api.Tests.Config;

namespace E2E.Api.Tests.Fixtures
{
    public class ProcedureFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly ProcedureService _procedures;
        private readonly List<Guid> _createdIds = new();

        public ProcedureFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _procedures = new ProcedureService(client);
        }

        public async Task<Guid> SetupAsync()
        {
            var procedurePayload = new Dictionary<string, object>
            {
                ["leap_name"] = $"Test Procedure {_config.CorrelationId}",
                ["statecode"] = _config.DefaultStateCode,
                ["statuscode"] = _config.DefaultStatusCode,
            };

            var id = await _procedures.CreateAsync(procedurePayload);
            _createdIds.Add(id);
            return id;
        }

        public async Task CleanupAsync()
        {
            foreach (var id in _createdIds)
                await _procedures.DeleteAsync(id);

            _createdIds.Clear();
        }
    }
}
