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
                ["statuscode"] = cfg.StatusCode,
                ["ownerid@odata.bind"] = $"/systemusers({cfg.OwnerId})",
                ["leap_SalesRepContractId@odata.bind"] =
                    $"/leap_salesrepcontracts({cfg.SalesRepContractId})",
                ["leap_name"] = cfg.Name,
            };

            if (cfg.ProductId.HasValue)
                payload["leap_RuleProduct@odata.bind"] = $"/products({cfg.ProductId.Value})";

            if (cfg.ProductFamilyId.HasValue)
                payload["leap_Product@odata.bind"] = $"/products({cfg.ProductFamilyId.Value})";

            if (cfg.PhysicianId.HasValue)
                payload["leap_Physician@odata.bind"] = $"/contacts({cfg.PhysicianId.Value})";

            if (cfg.HospitalId.HasValue)
                payload["leap_Hospital@odata.bind"] = $"/accounts({cfg.HospitalId.Value})";

            if (cfg.EffectiveStartDate.HasValue)
                payload["leap_effectivestartdate"] = cfg.EffectiveStartDate.Value.ToString(
                    "yyyy-MM-dd"
                );

            if (cfg.EffectiveEndDate.HasValue)
                payload["leap_effectiveenddate"] = cfg.EffectiveEndDate.Value.ToString(
                    "yyyy-MM-dd"
                );

            if (cfg.CommissionPercent.HasValue)
                payload["leap_commissonpercent"] = cfg.CommissionPercent.Value;

            if (cfg.CommissionAmount.HasValue)
                payload["leap_commissionamount"] = cfg.CommissionAmount.Value;

            if (cfg.CommissionRuleNotes != null)
                payload["leap_commissionsrulenotes"] = cfg.CommissionRuleNotes;

            if (cfg.SalesCategory != null)
                payload["leap_salescategory"] = cfg.SalesCategory;

            var id = await _commissionRules.CreateAsync(payload);
            _createdIds.Add(id);
            return id;
        }

        public async Task UpdateAsync(Guid id, SalesRepCommissionRuleUpdateConfig cfg)
        {
            var payload = new Dictionary<string, object?>();

            if (cfg.StatusCode.HasValue)
                payload["statuscode"] = cfg.StatusCode.Value;

            if (cfg.OwnerId.HasValue)
                payload["ownerid@odata.bind"] = $"/systemusers({cfg.OwnerId.Value})";

            if (cfg.SalesRepContractId.HasValue)
                payload["leap_SalesRepContractId@odata.bind"] =
                    $"/leap_salesrepcontracts({cfg.SalesRepContractId.Value})";

            if (cfg.Name != null)
                payload["leap_name"] = cfg.Name;

            if (cfg.ProductId.HasValue)
                payload["leap_RuleProduct@odata.bind"] = $"/products({cfg.ProductId.Value})";

            if (cfg.ProductFamilyId.HasValue)
                payload["leap_Product@odata.bind"] = $"/products({cfg.ProductFamilyId.Value})";

            if (cfg.PhysicianId.HasValue)
                payload["leap_Physician@odata.bind"] = $"/contacts({cfg.PhysicianId.Value})";

            if (cfg.HospitalId.HasValue)
                payload["leap_Hospital@odata.bind"] = $"/accounts({cfg.HospitalId.Value})";

            if (cfg.EffectiveStartDate.HasValue)
                payload["leap_effectivestartdate"] = cfg.EffectiveStartDate.Value.ToString(
                    "yyyy-MM-dd"
                );

            if (cfg.EffectiveEndDate.HasValue)
                payload["leap_effectiveenddate"] = cfg.EffectiveEndDate.Value.ToString(
                    "yyyy-MM-dd"
                );

            if (cfg.CommissionPercent.HasValue)
                payload["leap_commissonpercent"] = cfg.CommissionPercent.Value;

            if (cfg.CommissionAmount.HasValue)
                payload["leap_commissionamount"] = cfg.CommissionAmount.Value;

            if (cfg.CommissionRuleNotes != null)
                payload["leap_commissionsrulenotes"] = cfg.CommissionRuleNotes;

            if (cfg.SalesCategory != null)
                payload["leap_salescategory"] = cfg.SalesCategory;

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
