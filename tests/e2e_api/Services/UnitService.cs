using System;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class UnitService : DynamicsServiceBase<UnitService>
    {
        protected override string EntityName => EntityNames.Unit;

        public UnitService(DataverseClient client)
            : base(client) { }

        public Task<Guid> GetByNameAsync(string name) =>
            Client.GetByNameAsync(EntityName, nameField: "name", nameValue: name, idField: "uomid");
    }
}
