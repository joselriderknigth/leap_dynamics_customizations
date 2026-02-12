using System;
using System.Text.Json;
using System.Threading.Tasks;

using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class SalesRepCommissionRuleService : DynamicsServiceBase<SalesRepCommissionRuleService>
    {
        protected override string EntityName => EntityNames.SalesRepCommissionRule;
        public SalesRepCommissionRuleService(DataverseClient client) : base(client) { }
    }
}
