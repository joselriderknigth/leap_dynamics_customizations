using System;
using System.Text.Json;
using System.Threading.Tasks;

using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class ProductService : DynamicsServiceBase<ProductService>
    {
        protected override string EntityName => EntityNames.Product;
        public ProductService(DataverseClient client) : base(client) { }
    }
}
