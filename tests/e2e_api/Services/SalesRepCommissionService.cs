using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class SalesRepCommissionService : DynamicsServiceBase<SalesRepCommissionService>
    {
        protected override string EntityName => EntityNames.SalesRepCommission;

        public SalesRepCommissionService(DataverseClient client)
            : base(client) { }

        public async Task<List<JsonElement>> GetByCaseAsync(Guid caseId)
        {
            var fetchXml = FetchXmlUtils
                .LoadFetchXml("FetchXml/SalesRepCommissionsByCase.xml")
                .Replace("{CASE_ID}", caseId.ToString());
            return await Client.GetMultipleByFetchXmlAsync("leap_salesrepcommissions", fetchXml);
        }

        public async Task DeleteByCaseIdAsync(Guid caseId)
        {
            var commissions = await GetByCaseAsync(caseId);
            foreach (var commission in commissions)
            {
                var id = commission.GetProperty("leap_salesrepcommissionid").GetGuid();
                await DeleteAsync(id);
            }
        }
    }
}
