using System;

namespace E2E.Api.Tests.Config
{
    public record PricingTierConfig
    {
        public required Guid SupplierContractId { get; init; }
        public required Guid ProductId { get; init; }
        public required Guid OwnerId { get; init; }
        public required decimal PriceLowerBound { get; init; }
        public required decimal PriceUpperBound { get; init; }
        public decimal? CommissionRate { get; init; }
        public int StatusCode { get; init; } = 1;
        public string? Name { get; init; }

        public static PricingTierConfig Default(
            Guid supplierContractId,
            Guid productId,
            Guid ownerId,
            string? name = null,
            decimal priceLowerBound = 0,
            decimal priceUpperBound = 100000,
            int statusCode = 1
        )
        {
            return new PricingTierConfig
            {
                SupplierContractId = supplierContractId,
                ProductId = productId,
                OwnerId = ownerId,
                PriceLowerBound = priceLowerBound,
                PriceUpperBound = priceUpperBound,
                Name = name,
                StatusCode = statusCode,
            };
        }
    }
}
