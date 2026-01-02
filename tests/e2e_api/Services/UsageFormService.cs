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
    }
}
