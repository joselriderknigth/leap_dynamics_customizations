using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class SalesAdjustmentService : DynamicsServiceBase<SalesAdjustmentService>
    {
        protected override string EntityName => EntityNames.SalesAdjustment;

        public SalesAdjustmentService(DataverseClient client)
            : base(client) { }

        public async Task<List<JsonElement>> GetByCaseAsync(Guid caseId)
        {
            var fetchXml = FetchXmlUtils
                .LoadFetchXml("FetchXml/SalesAdjustmentsByCase.xml")
                .Replace("{CASE_ID}", caseId.ToString());
            return await Client.GetMultipleByFetchXmlAsync("leap_salesadjustments", fetchXml);
        }

        public async Task DeleteByCaseIdAsync(Guid caseId)
        {
            var adjustments = await GetByCaseAsync(caseId);
            foreach (var adjustment in adjustments)
            {
                var id = adjustment.GetProperty("leap_salesadjustmentid").GetGuid();
                await DeleteAsync(id);
            }
        }
    }
}
