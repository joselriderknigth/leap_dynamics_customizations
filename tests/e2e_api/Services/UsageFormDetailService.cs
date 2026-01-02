using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class UsageFormDetailService : DynamicsServiceBase<UsageFormDetailService>
    {
        protected override string EntityName => EntityNames.UsageForm;

        public UsageFormDetailService(DataverseClient client)
            : base(client) { }

        public async Task<List<JsonElement>> GetByUsageFormAsync(Guid usageFormId)
        {
            var fetchXml = FetchXmlUtils
                .LoadFetchXml("FetchXml/UsageFormDetailsByUsageForm.xml")
                .Replace("{USAGE_FORM_ID}", usageFormId.ToString());
            return await Client.GetMultipleByFetchXmlAsync("leap_usageformdetails", fetchXml);
        }
    }
}
