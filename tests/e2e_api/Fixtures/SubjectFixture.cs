using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Fixtures
{
    public class SubjectFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly SubjectService _subjects;
        private readonly List<Guid> _createdIds = new();

        public SubjectFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _subjects = new SubjectService(client);
        }

        public async Task<Guid> SetupAsync(Guid organizationId)
        {
            var subjectPayload = new Dictionary<string, object>
            {
                ["title"] = $"Test Subject {_config.CorrelationId}",
                ["OrganizationId@odata.bind"] = $"organizations/({organizationId})",
            };

            var id = await _subjects.CreateAsync(subjectPayload);
            _createdIds.Add(id);
            return id;
        }

        public async Task CleanupAsync()
        {
            foreach (var id in _createdIds)
                await _subjects.DeleteAsync(id);

            _createdIds.Clear();
        }
    }
}
