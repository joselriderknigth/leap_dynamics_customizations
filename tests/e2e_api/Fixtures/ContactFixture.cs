using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;
using E2E.Api.Tests.Config;

namespace E2E.Api.Tests.Fixtures
{
    public class ContactFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly ContactService _contacts;
        private readonly List<Guid> _createdIds = new();

        public ContactFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _contacts = new ContactService(client);
        }

        public async Task<Guid> SetupPhysicianAsync()
        {
            var contactPayload = new Dictionary<string, object>
            {
                ["firstname"] = $"Test Physician",
                ["middlename"] = _config.CorrelationId,
                ["lastname"] = "Fixture",
                ["customertypecode"] = OptionSetValues.Contact.CustomerTypeCode.Physician,
                ["leap_salutation"] = OptionSetValues.Contact.Salutation.Dr,
                ["ownerid@odata.bind"] = $"/systemusers({_config.OwnerUserId})",
                ["statecode"] = _config.DefaultStateCode,
                ["statuscode"] = _config.DefaultStatusCode,
            };
            
            var id = await _contacts.CreateAsync(contactPayload);
            _createdIds.Add(id);
            return id;
        }

        public async Task CleanupAsync()
        {
            foreach (var id in _createdIds)
                await _contacts.DeleteAsync(id);

            _createdIds.Clear();
        }
    }
}
