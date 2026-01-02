using System;
using System.Text.Json;
using System.Threading.Tasks;

using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class CaseProductService : DynamicsServiceBase<CaseProductService>
    {
        protected override string EntityName => EntityNames.CaseProduct;
        public CaseProductService(DataverseClient client) : base(client) { }
    }
}
