using System;
using System.Text.Json;
using System.Threading.Tasks;

using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class SupplierContractService : DynamicsServiceBase<SupplierContractService>
    {
        protected override string EntityName => EntityNames.SupplierContract;
        public SupplierContractService(DataverseClient client) : base(client) { }
    }
}
