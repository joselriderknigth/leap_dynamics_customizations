using System;

namespace E2E.Api.Tests.Config
{
    public record SalesRepContractConfig
    {
        // Required fields
        public required int ContractType { get; init; }
        public required int StatusCode { get; init; }
        public required decimal MaxCommissionRate { get; init; }
        public required decimal MinCommissionRate { get; init; }
        public required DateTime StartDate { get; init; }
        public required Guid OwnerId { get; init; }

        // Optional fields
        public string? ContractNumber { get; init; }
        public string? Name { get; init; }
        public int? RepRole { get; init; }
        public bool? Salaried { get; init; }
        public DateTime? EndDate { get; init; }
        public Guid? ExternalRepId { get; init; }
        public Guid? DistributorId { get; init; }
        public Guid? ExternalAgentId { get; init; }
        public Guid? SalesRepId { get; init; }

        // Custom config options
        public bool? IsOwnedByTeam { get; init; }

        public static SalesRepContractConfig Default(
            int contractType,
            int statusCode,
            decimal minCommissionRate,
            decimal maxCommissionRate,
            DateTime startDate,
            Guid ownerId
        )
        {
            return new SalesRepContractConfig
            {
                ContractType = contractType,
                StatusCode = statusCode,
                MinCommissionRate = minCommissionRate,
                MaxCommissionRate = maxCommissionRate,
                StartDate = startDate,
                OwnerId = ownerId,
            };
        }
    }
}
