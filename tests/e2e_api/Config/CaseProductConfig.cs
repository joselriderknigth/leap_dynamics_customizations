using System;

public record CaseProductConfig
{
    // Relationships
    public Guid CaseId { get; init; }
    public Guid HospitalId { get; init; }
    public Guid SupplierId { get; init; }
    public Guid ManufacturerId { get; init; }
    public Guid ProductFamilyId { get; init; }
    public Guid ProductId { get; init; }

    // Core behavior flags
    public int ProductType { get; init; }
    public bool IsHospitalOwned { get; init; } = false;
    public bool IsIO { get; init; } = false;
    public bool IsRestock { get; init; } = false;

    // Pricing
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal IOUnitPrice { get; init; }
    public int IOQuantity { get; init; }


    public static CaseProductConfig Default(
        Guid caseId,
        Guid hospitalId,
        Guid supplierId,
        Guid manufacturerId,
        Guid productFamilyId,
        Guid productId
    )
    {
        return new CaseProductConfig
        {
            CaseId = caseId,
            HospitalId = hospitalId,
            SupplierId = supplierId,
            ManufacturerId = manufacturerId,
            ProductFamilyId = productFamilyId,
            ProductId = productId,
        };
    }
}
