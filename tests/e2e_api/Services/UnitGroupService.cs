using System;
using System.Text.Json;
using System.Threading.Tasks;

using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class UnitGroupService : DynamicsServiceBase<UnitGroupService>
    {
        protected override string EntityName => EntityNames.UnitGroup;
        public UnitGroupService(DataverseClient client) : base(client) { }

        public Task<Guid> GetByNameAsync(string name) => Client.GetByNameAsync(
            EntityName,
            nameField: "name",
            nameValue: name,
            idField: "uomscheduleid");
    }
}
