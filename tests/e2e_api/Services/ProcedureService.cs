using System;
using System.Text.Json;
using System.Threading.Tasks;

using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class ProcedureService : DynamicsServiceBase<ProcedureService>
    {
        protected override string EntityName => EntityNames.Procedure;
        public ProcedureService(DataverseClient client) : base(client) { }
    }
}
