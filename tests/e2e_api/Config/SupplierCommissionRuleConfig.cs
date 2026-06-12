using System;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Config
{
    public record SupplierCommissionRuleConfig
    {
        // Required fields
        public required int StatusCode { get; init; }
        public required Guid OwnerId { get; init; }
        public required Guid SupplierContractId { get; init; }
        public required string Name { get; init; }

        // Optional lookup fields
        public Guid? ProductId { get; init; }
        public Guid? ProductFamilyId { get; init; }
        public Guid? PhysicianId { get; init; }
        public Guid? HospitalId { get; init; }

        // Optional date fields
        public DateTime? EffectiveStartDate { get; init; }
        public DateTime? EffectiveEndDate { get; init; }

        // Optional commission fields
        public decimal? CommissionPercent { get; init; }
        public decimal? CommissionAmount { get; init; }

        // Optional hospital owned
        public bool? HospitalOwnedOverride { get; init; }

        public static SupplierCommissionRuleConfig Default(
            string name,
            Guid supplierContractId,
            Guid ownerId,
            int statusCode = 1
        )
        {
            return new SupplierCommissionRuleConfig
            {
                StatusCode = statusCode,
                Name = name,
                SupplierContractId = supplierContractId,
                OwnerId = ownerId,
            };
        }
    }

    public record SupplierCommissionRuleUpdateConfig
    {
        public int? StatusCode { get; init; }
        public Guid? OwnerId { get; init; }
        public Guid? SupplierContractId { get; init; }
        public string? Name { get; init; }

        // Optional lookup fields
        public Guid? ProductId { get; init; }
        public Guid? ProductFamilyId { get; init; }
        public Guid? PhysicianId { get; init; }
        public Guid? HospitalId { get; init; }

        // Optional date fields
        public DateTime? EffectiveStartDate { get; init; }
        public DateTime? EffectiveEndDate { get; init; }

        // Optional commission fields
        public decimal? CommissionPercent { get; init; }
        public decimal? CommissionAmount { get; init; }

        
        // Optional hospital owned with explicit null support
        public Optional<bool> HospitalOwnedOverride { get; init; }
    }
}
