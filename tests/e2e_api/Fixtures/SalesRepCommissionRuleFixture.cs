using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Fixtures
{
    public class SalesRepCommissionRuleFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly SalesRepCommissionRuleService _commissionRules;
        private readonly List<Guid> _createdIds = new();

        public SalesRepCommissionRuleFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _commissionRules = new SalesRepCommissionRuleService(client);
        }

        public async Task<Guid> SetupAsync(SalesRepCommissionRuleConfig cfg)
        {
            var payload = new Dictionary<string, object>
            {
                ["statecode"] = cfg.StatusCode,
                ["leap_commissionruletype"] = cfg.CommissionRuleType,
                ["leap_commissionsplit"] = cfg.CommissionSplit,
                ["leap_startdate"] = cfg.StartDate.ToString("yyyy-MM-dd"),
                ["ownerid@odata.bind"] = $"/systemusers({cfg.OwnerId})",
                ["leap_SalesRepContractId@odata.bind"] =
                    $"/leap_salesrepcontracts({cfg.SalesRepContractId})",
            };

            if (cfg.IsOwnedByTeam.HasValue && cfg.IsOwnedByTeam.Value)
            {
                payload["ownerid@odata.bind"] = $"/teams({cfg.OwnerId})";
            }

            if (cfg.RepRole.HasValue)
                payload["leap_reprole"] = cfg.RepRole.Value;

            if (cfg.SalesCategory.HasValue)
                payload["leap_salescategory"] = cfg.SalesCategory.Value;

            if (cfg.Name != null)
                payload["leap_name"] = cfg.Name;

            if (cfg.RequiredSalesRepMatch.HasValue)
                payload["leap_requiresalesrepmatch"] = cfg.RequiredSalesRepMatch.Value;

            if (cfg.OverrideRate.HasValue)
                payload["leap_overriderate"] = cfg.OverrideRate.Value;

            if (cfg.EndDate.HasValue)
                payload["leap_enddate"] = cfg.EndDate.Value.ToString("yyyy-MM-dd");

            if (cfg.HospitalId.HasValue)
                payload["leap_Account@odata.bind"] = $"/accounts({cfg.HospitalId.Value})";

            if (cfg.BusinessUnitId.HasValue)
                payload["leap_BusinessUnit@odata.bind"] = $"/businessunits({cfg.BusinessUnitId.Value})";

            if (cfg.DistributorId.HasValue)
                payload["leap_DistributorId@odata.bind"] = $"/accounts({cfg.DistributorId.Value})";

            if (cfg.ExternalAgentId.HasValue)
                payload["leap_ExternalAgent@odata.bind"] = $"/systemusers({cfg.ExternalAgentId.Value})";

            if (cfg.ExternalRepId.HasValue)
                payload["leap_ExternalSalesRepId@odata.bind"] = $"/contacts({cfg.ExternalRepId.Value})";

            if (cfg.ManufacturerId.HasValue)
                payload["leap_Manufacturer@odata.bind"] = $"/accounts({cfg.ManufacturerId.Value})";

            if (cfg.PhysicianId.HasValue)
                payload["leap_Physician@odata.bind"] = $"/contacts({cfg.PhysicianId.Value})";

            if (cfg.ProductId.HasValue)
                payload["leap_Product@odata.bind"] = $"/products({cfg.ProductId.Value})";

            if (cfg.ProductFamilyId.HasValue)
                payload["leap_ProductFamily@odata.bind"] = $"/products({cfg.ProductFamilyId.Value})";

            if (cfg.SalesRepId.HasValue)
                payload["leap_SalesRep@odata.bind"] = $"/systemusers({cfg.SalesRepId.Value})";

            if (cfg.SubjectId.HasValue)
                payload["leap_Subject@odata.bind"] = $"/leap_subjects({cfg.SubjectId.Value})";

            if (cfg.SupplierId.HasValue)
                payload["leap_Supplier@odata.bind"] = $"/accounts({cfg.SupplierId.Value})";

            if (cfg.TeamId.HasValue)
                payload["leap_Team@odata.bind"] = $"/teams({cfg.TeamId.Value})";

            if (cfg.TerritoryId.HasValue)
                payload["leap_Territory@odata.bind"] = $"/leap_territories({cfg.TerritoryId.Value})";

            var id = await _commissionRules.CreateAsync(payload);
            _createdIds.Add(id);
            return id;
        }

        public async Task UpdateAsync(Guid id, SalesRepCommissionRuleUpdateConfig cfg)
        {
            var payload = new Dictionary<string, object?>();

            if (cfg.StatusCode.HasValue)
                payload["statecode"] = cfg.StatusCode.Value;

            if (cfg.CommissionRuleType.HasValue)
                payload["leap_CommissionRuleType"] = cfg.CommissionRuleType.Value;

            if (cfg.CommissionSplit.HasValue)
                payload["leap_CommissionSplit"] = cfg.CommissionSplit.Value;

            if (cfg.StartDate.HasValue)
                payload["leap_StartDate"] = cfg.StartDate.Value.ToString("yyyy-MM-dd");

            if (cfg.OwnerId.HasValue)
                payload["OwnerId@odata.bind"] = $"/systemusers({cfg.OwnerId.Value})";

            if (cfg.SalesRepContractId.HasValue)
                payload["leap_SalesRepContractId@odata.bind"] =
                    $"/leap_salesrepcontracts({cfg.SalesRepContractId.Value})";

            if (cfg.RepRole.HasValue)
                payload["leap_RepRole"] = cfg.RepRole.Value;

            if (cfg.SalesCategory.HasValue)
                payload["leap_SalesCategory"] = cfg.SalesCategory.Value;

            if (cfg.Name != null)
                payload["leap_name"] = cfg.Name;

            if (cfg.RequiredSalesRepMatch.HasValue)
                payload["leap_RequireSalesRepMatch"] = cfg.RequiredSalesRepMatch.Value;

            if (cfg.OverrideRate.HasValue)
                payload["leap_OverrideRate"] = cfg.OverrideRate.Value;

            if (cfg.EndDate.HasValue)
                payload["leap_EndDate"] = cfg.EndDate.Value.ToString("yyyy-MM-dd");

            if (cfg.HospitalId.HasValue)
                payload["leap_Account@odata.bind"] = $"/accounts({cfg.HospitalId.Value})";

            if (cfg.BusinessUnitId.HasValue)
                payload["leap_BusinessUnit@odata.bind"] = $"/businessunits({cfg.BusinessUnitId.Value})";

            if (cfg.DistributorId.HasValue)
                payload["leap_DistributorId@odata.bind"] = $"/accounts({cfg.DistributorId.Value})";

            if (cfg.ExternalAgentId.HasValue)
                payload["leap_ExternalAgent@odata.bind"] = $"/accounts({cfg.ExternalAgentId.Value})";

            if (cfg.ExternalRepId.HasValue)
                payload["leap_ExternalSalesRepId@odata.bind"] = $"/contacts({cfg.ExternalRepId.Value})";

            if (cfg.ManufacturerId.HasValue)
                payload["leap_Manufacturer@odata.bind"] = $"/accounts({cfg.ManufacturerId.Value})";

            if (cfg.PhysicianId.HasValue)
                payload["leap_Physician@odata.bind"] = $"/contacts({cfg.PhysicianId.Value})";

            if (cfg.ProductId.HasValue)
                payload["leap_Product@odata.bind"] = $"/products({cfg.ProductId.Value})";

            if (cfg.ProductFamilyId.HasValue)
                payload["leap_ProductFamily@odata.bind"] = $"/products({cfg.ProductFamilyId.Value})";

            if (cfg.SalesRepId.HasValue)
                payload["leap_SalesRep@odata.bind"] = $"/systemusers({cfg.SalesRepId.Value})";

            if (cfg.SubjectId.HasValue)
                payload["leap_Subject@odata.bind"] = $"/leap_subjects({cfg.SubjectId.Value})";

            if (cfg.SupplierId.HasValue)
                payload["leap_Supplier@odata.bind"] = $"/accounts({cfg.SupplierId.Value})";

            if (cfg.TeamId.HasValue)
                payload["leap_Team@odata.bind"] = $"/teams({cfg.TeamId.Value})";

            if (cfg.TerritoryId.HasValue)
                payload["leap_Territory@odata.bind"] = $"/leap_territories({cfg.TerritoryId.Value})";

            await _commissionRules.UpdateAsync(id, payload);
        }

        public async Task CleanupAsync()
        {
            foreach (var id in _createdIds)
                await _commissionRules.DeleteAsync(id);

            _createdIds.Clear();
        }
    }
}
