using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class UsageFormService : DynamicsServiceBase<UsageFormService>
    {
        protected override string EntityName => EntityNames.UsageForm;

        public UsageFormService(DataverseClient client)
            : base(client) { }

        public async Task<List<JsonElement>> GetBySupplierAsync(Guid caseId, Guid supplierId)
        {
            var fetchXml = FetchXmlUtils
                .LoadFetchXml("FetchXml/UsageFormsBySupplier.xml")
                .Replace("{CASE_ID}", caseId.ToString())
                .Replace("{SUPPLIER_ID}", supplierId.ToString());
            return await Client.GetMultipleByFetchXmlAsync("leap_usageforms", fetchXml);
        }

        public async Task<List<JsonElement>> GetByCaseAsync(Guid caseId)
        {
            var fetchXml = FetchXmlUtils
                .LoadFetchXml("FetchXml/UsageFormsByCase.xml")
                .Replace("{CASE_ID}", caseId.ToString());
            return await Client.GetMultipleByFetchXmlAsync("leap_usageforms", fetchXml);
        }

        public async Task DeleteByCaseIdAsync(Guid caseId)
        {
            var usageForms = await GetByCaseAsync(caseId);
            foreach (var form in usageForms)
            {
                var id = form.GetProperty("leap_usageformid").GetGuid();
                await DeleteAsync(id);
            }
        }
    }
}
