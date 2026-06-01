using System;

namespace E2E.Api.Tests.Config
{
    public record UsageFormDetailConfig
    {
        // Owner and Business Unit fields for RequiredSalesRepMatch comparison
        public Guid? OwnerId { get; init; }
        public Guid? OwningBusinessUnitId { get; init; }
    }
}
