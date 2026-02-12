using System;
using System.Text.Json;
using System.Threading.Tasks;

using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class SalesAdjustmentRuleService : DynamicsServiceBase<SalesAdjustmentRuleService>
    {
        protected override string EntityName => EntityNames.SalesAdjustmentRule;
        public SalesAdjustmentRuleService(DataverseClient client) : base(client) { }
    }
}
