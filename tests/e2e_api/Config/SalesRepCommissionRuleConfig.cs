using System;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Config
{
    public record SalesRepCommissionRuleConfig
    {
        // Required fields
        public required int StatusCode { get; init; }
        public required Guid OwnerId { get; init; }
        public required Guid SalesRepContractId { get; init; }
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
        public string? CommissionRuleNotes { get; init; }

        // Optional sales category
        public string? SalesCategory { get; init; }

        public static SalesRepCommissionRuleConfig Default(
            string name,
            Guid salesRepContractId,
            Guid ownerId,
            int statusCode = 1
        )
        {
            return new SalesRepCommissionRuleConfig
            {
                StatusCode = statusCode,
                Name = name,
                SalesRepContractId = salesRepContractId,
                OwnerId = ownerId,
            };
        }
    }

    public record SalesRepCommissionRuleUpdateConfig
    {
        public int? StatusCode { get; init; }
        public Guid? OwnerId { get; init; }
        public Guid? SalesRepContractId { get; init; }
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
        public string? CommissionRuleNotes { get; init; }

        // Optional sales category
        public string? SalesCategory { get; init; }
    }
}
