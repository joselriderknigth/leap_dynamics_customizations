using System;

namespace E2E.Api.Tests.Config
{
    public record SupplierContractConfig
    {
        // Required fields
        public required int StatusCode { get; init; }
        public required string Name { get; init; }
        public required Guid SupplierId { get; init; }
        public required Guid EntityId { get; init; }
        public required DateTime EffectiveStartDate { get; init; }
        public required Guid OwnerId { get; init; }

        // Optional fields
        public DateTime? EffectiveEndDate { get; init; }
        public Guid? CommissionPayeeId { get; init; }
        public bool? AllPhysicians { get; init; }
        public bool? AllHospitals { get; init; }

        public static SupplierContractConfig Default(
            string name,
            Guid supplierId,
            Guid entityId,
            DateTime effectiveStartDate,
            Guid ownerId,
            int statusCode = 1
        )
        {
            return new SupplierContractConfig
            {
                StatusCode = statusCode,
                Name = name,
                SupplierId = supplierId,
                EntityId = entityId,
                EffectiveStartDate = effectiveStartDate,
                OwnerId = ownerId,
            };
        }
    }
}
