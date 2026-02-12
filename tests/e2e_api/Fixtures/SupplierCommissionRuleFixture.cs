using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Fixtures
{
    public class SupplierCommissionRuleFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly SupplierCommissionRuleService _commissionRules;
        private readonly List<Guid> _createdIds = new();

        public SupplierCommissionRuleFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _commissionRules = new SupplierCommissionRuleService(client);
        }

        public async Task<Guid> SetupAsync(SupplierCommissionRuleConfig cfg)
        {
            var payload = new Dictionary<string, object>
            {
                ["statuscode"] = cfg.StatusCode,
                ["ownerid@odata.bind"] = $"/systemusers({cfg.OwnerId})",
                ["leap_SupplierContractId@odata.bind"] =
                    $"/leap_suppliercontracts({cfg.SupplierContractId})",
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

            if (cfg.HospitalOwnedOverride.HasValue)
                payload["leap_hospitalownedoverride"] = cfg.HospitalOwnedOverride.Value;

            var id = await _commissionRules.CreateAsync(payload);
            _createdIds.Add(id);
            return id;
        }

        public async Task UpdateAsync(Guid id, SupplierCommissionRuleUpdateConfig cfg)
        {
            var payload = new Dictionary<string, object?>();

            if (cfg.StatusCode.HasValue)
                payload["statuscode"] = cfg.StatusCode.Value;

            if (cfg.OwnerId.HasValue)
                payload["ownerid@odata.bind"] = $"/systemusers({cfg.OwnerId.Value})";

            if (cfg.SupplierContractId.HasValue)
                payload["leap_SupplierContractId@odata.bind"] =
                    $"/leap_suppliercontracts({cfg.SupplierContractId.Value})";

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

            // Handle Optional<bool> for HospitalOwnedOverride
            if (cfg.HospitalOwnedOverride.IsDefined)
            {
                if (cfg.HospitalOwnedOverride.IsNull)
                    payload["leap_hospitalownedoverride"] = null;
                else
                    payload["leap_hospitalownedoverride"] = cfg.HospitalOwnedOverride.Value;
            }

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
