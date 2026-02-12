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
        protected override string EntityName => EntityNames.UsageFormDetail;

        public UsageFormDetailService(DataverseClient client)
            : base(client) { }

        public async Task<List<JsonElement>> GetByUsageFormAsync(Guid usageFormId)
        {
            var fetchXml = FetchXmlUtils
                .LoadFetchXml("FetchXml/UsageFormDetailsByUsageForm.xml")
                .Replace("{USAGE_FORM_ID}", usageFormId.ToString());
            return await Client.GetMultipleByFetchXmlAsync("leap_usageformdetails", fetchXml);
        }

        public async Task<List<JsonElement>> GetByCaseAsync(Guid caseId)
        {
            var fetchXml = FetchXmlUtils
                .LoadFetchXml("FetchXml/UsageFormDetailsByCase.xml")
                .Replace("{CASE_ID}", caseId.ToString());
            return await Client.GetMultipleByFetchXmlAsync("leap_usageformdetails", fetchXml);
        }

        public async Task DeleteByCaseIdAsync(Guid caseId)
        {
            var usageFormDetails = await GetByCaseAsync(caseId);
            foreach (var detail in usageFormDetails)
            {
                var id = detail.GetProperty("leap_usageformdetailid").GetGuid();
                await DeleteAsync(id);
            }
        }
    }
}
