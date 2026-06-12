using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class PricingTierService : DynamicsServiceBase<PricingTierService>
    {
        protected override string EntityName => EntityNames.PricingTier;

        public PricingTierService(DataverseClient client)
            : base(client) { }

       /* public async Task<List<JsonElement>> GetMatchingAsync(
            Guid supplierContractId,
            Guid productId,
            decimal unitPrice
        )
        {
            var fetchXml = FetchXmlUtils.LoadFetchXml("FetchXml/GetPrincigTiers.xml")
                .Replace("{SUPPLIERCONTRACT_ID}", supplierContractId.ToString())
                .Replace("{PRODUCT_ID}", productId.ToString())
                .Replace(
                    "{UNIT_PRICE}",
                    unitPrice.ToString("0.00", CultureInfo.InvariantCulture)
                );

            return await Client.GetMultipleByFetchXmlAsync("leap_pricingtiers", fetchXml);
        } */
    }
}
