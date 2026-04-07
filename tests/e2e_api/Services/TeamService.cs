using System;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class TeamService : DynamicsServiceBase<TeamService>
    {
        protected override string EntityName => EntityNames.Team;

        public TeamService(DataverseClient client)
            : base(client) { }
    }
}
