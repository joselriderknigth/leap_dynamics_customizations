using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class SupplierCommissionService : DynamicsServiceBase<SupplierCommissionService>
    {
        protected override string EntityName => EntityNames.SupplierCommission;

        public SupplierCommissionService(DataverseClient client)
            : base(client) { }

        public async Task<List<JsonElement>> GetByCaseAsync(Guid caseId)
        {
            var fetchXml = FetchXmlUtils
                .LoadFetchXml("FetchXml/SupplierCommissionsByCase.xml")
                .Replace("{CASE_ID}", caseId.ToString());
            return await Client.GetMultipleByFetchXmlAsync("leap_suppliercommissions", fetchXml);
        }

        public async Task DeleteByCaseIdAsync(Guid caseId)
        {
            var commissions = await GetByCaseAsync(caseId);
            foreach (var commission in commissions)
            {
                var id = commission.GetProperty("leap_suppliercommissionid").GetGuid();
                await DeleteAsync(id);
            }
        }
    }
}
