using System;

namespace E2E.Api.Tests.Config
{
    public class FixtureConfig
    {
        public EnvironmentConfig Environment { get; set; } = null!;
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
        public Guid OwnerUserId { get; set; }
        public int DefaultStateCode { get; set; } = 0;
        public int DefaultStatusCode { get; set; } = 1;

        public int DefaultAccountType { get; set; } = 293360001;
        public int DefaultRelationshipType { get; set; } = 293360002;
    }
}