using System;

namespace E2E.Api.Tests.Config
{
    public record SalesRepContractConfig
    {
        // Required fields
        public required int StatusCode { get; init; }
        public required string Name { get; init; }
        public required Guid SalesRepId { get; init; }
        public required Guid EntityId { get; init; }
        public required DateTime EffectiveStartDate { get; init; }
        public required Guid OwnerId { get; init; }

        // Optional fields
        public DateTime? EffectiveEndDate { get; init; }
        public Guid? CommissionPayeeId { get; init; }
        public string? SalesCategory { get; init; }
        public decimal? CommissionSplit { get; init; }
        public bool? AllPhysicians { get; init; }
        public bool? AllHospitals { get; init; }

        public static SalesRepContractConfig Default(
            string name,
            Guid salesRepId,
            Guid entityId,
            DateTime effectiveStartDate,
            Guid ownerId,
            int statusCode = 1
        )
        {
            return new SalesRepContractConfig
            {
                StatusCode = statusCode,
                Name = name,
                SalesRepId = salesRepId,
                EntityId = entityId,
                EffectiveStartDate = effectiveStartDate,
                OwnerId = ownerId,
            };
        }
    }
}
