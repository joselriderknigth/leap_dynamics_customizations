using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Fixtures
{
    public class ProductFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly ProductService _products;
        private readonly Stack<Guid> _createdIds = new();

        public ProductFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _products = new ProductService(client);
        }

        public async Task<Guid> SetupProductAsync(
            Guid organizationId,
            Guid subjectId,
            Guid unitGroupId,
            Guid unitId,
            Guid supplier,
            Guid productFamily,
            int productType,
            decimal? costPrice = null
        )
        {
            var productPayload = new Dictionary<string, object>
            {
                ["subjectid@odata.bind"] = $"/subjects({subjectId})",
                ["statecode"] = _config.DefaultStateCode,
                ["quantitydecimal"] = 2,
                ["productstructure"] = OptionSetValues.Product.ProductStructure.Product,
                ["producttypecode"] = productType,
                ["organizationid@odata.bind"] = $"/organizations({organizationId})",
                ["name"] = $"Test Product {_config.CorrelationId}",
                ["statuscode"] = _config.DefaultStatusCode,
                ["defaultuomscheduleid@odata.bind"] = $"/uomschedules({unitGroupId})",
                ["defaultuomid@odata.bind"] = $"/uoms({unitId})",
                ["leap_Supplier@odata.bind"] = $"/accounts({supplier})",
                ["leap_Manufacturer@odata.bind"] = $"/accounts({supplier})",
                ["parentproductid@odata.bind"] = $"/products({productFamily})",
            };

            if (costPrice.HasValue)
                productPayload["leap_leapstandardcost"] = costPrice.Value;

            var id = await _products.CreateAsync(productPayload);
            _createdIds.Push(id);
            return id;
        }

        public async Task<Guid> SetupProductFamilyAsync(
            Guid organizationId,
            Guid subjectId,
            Guid unitGroupId,
            Guid unitId,
            Guid supplier,
            int productType
        )
        {
            var productPayload = new Dictionary<string, object>
            {
                ["subjectid@odata.bind"] = $"/subjects({subjectId})",
                ["statecode"] = _config.DefaultStateCode,
                ["quantitydecimal"] = 2,
                ["productstructure"] = OptionSetValues.Product.ProductStructure.ProductFamily,
                ["producttypecode"] = productType,
                ["organizationid@odata.bind"] = $"/organizations({organizationId})",
                ["name"] = $"Test Product Family {_config.CorrelationId}",
                ["statuscode"] = _config.DefaultStatusCode,
                ["defaultuomscheduleid@odata.bind"] = $"/uomschedules({unitGroupId})",
                ["defaultuomid@odata.bind"] = $"/uoms({unitId})",
                ["leap_Supplier@odata.bind"] = $"/accounts({supplier})",
                ["leap_Manufacturer@odata.bind"] = $"/accounts({supplier})",
            };

            var id = await _products.CreateAsync(productPayload);
            _createdIds.Push(id);
            return id;
        }

        public async Task CleanupAsync()
        {
            while (_createdIds.Count > 0)
            {
                var id = _createdIds.Pop();
                await _products.DeleteAsync(id);
            }
        }
    }
}
