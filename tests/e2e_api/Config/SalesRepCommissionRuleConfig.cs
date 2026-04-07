using System;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Config
{
    public record SalesRepCommissionRuleConfig
    {
        // Required fields
        public required int StatusCode { get; init; }
        public required int CommissionRuleType { get; init; }
        public required decimal CommissionSplit { get; init; }
        public required DateTime StartDate { get; init; }
        public required Guid OwnerId { get; init; }
        public required Guid SalesRepContractId { get; init; }

        // Optional fields
        public int? RepRole { get; init; }
        public int? SalesCategory { get; init; }
        public string? Name { get; init; }
        public bool? RequiredSalesRepMatch { get; init; }
        public decimal? OverrideRate { get; init; }
        public DateTime? EndDate { get; init; }
        public Guid? HospitalId { get; init; }
        public Guid? BusinessUnitId { get; init; }
        public Guid? DistributorId { get; init; }
        public Guid? ExternalAgentId { get; init; }
        public Guid? ExternalRepId { get; init; }
        public Guid? ManufacturerId { get; init; }
        public Guid? PhysicianId { get; init; }
        public Guid? ProductId { get; init; }
        public Guid? ProductFamilyId { get; init; }
        public Guid? SalesRepId { get; init; }
        public Guid? SubjectId { get; init; }
        public Guid? SupplierId { get; init; }
        public Guid? TeamId { get; init; }
        public Guid? TerritoryId { get; init; }

        // Custom config options
        public bool? IsOwnedByTeam { get; init; }

        public static SalesRepCommissionRuleConfig Default(
            int statusCode,
            int commissionRuleType,
            decimal commissionSplit,
            DateTime startDate,
            Guid ownerId,
            Guid salesRepContractId
        )
        {
            return new SalesRepCommissionRuleConfig
            {
                StatusCode = statusCode,
                CommissionRuleType = commissionRuleType,
                CommissionSplit = commissionSplit,
                StartDate = startDate,
                OwnerId = ownerId,
                SalesRepContractId = salesRepContractId,
            };
        }
    }

    public record SalesRepCommissionRuleUpdateConfig
    {
        public int? StatusCode { get; init; }
        public int? CommissionRuleType { get; init; }
        public decimal? CommissionSplit { get; init; }
        public DateTime? StartDate { get; init; }
        public Guid? OwnerId { get; init; }
        public Guid? SalesRepContractId { get; init; }

        // Optional fields
        public int? RepRole { get; init; }
        public int? SalesCategory { get; init; }
        public string? Name { get; init; }
        public bool? RequiredSalesRepMatch { get; init; }
        public decimal? OverrideRate { get; init; }
        public DateTime? EndDate { get; init; }
        public Guid? HospitalId { get; init; }
        public Guid? BusinessUnitId { get; init; }
        public Guid? DistributorId { get; init; }
        public Guid? ExternalAgentId { get; init; }
        public Guid? ExternalRepId { get; init; }
        public Guid? ManufacturerId { get; init; }
        public Guid? PhysicianId { get; init; }
        public Guid? ProductId { get; init; }
        public Guid? ProductFamilyId { get; init; }
        public Guid? SalesRepId { get; init; }
        public Guid? SubjectId { get; init; }
        public Guid? SupplierId { get; init; }
        public Guid? TeamId { get; init; }
        public Guid? TerritoryId { get; init; }
    }
}
