using System;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace E2E.Api.Tests.Tests
{
    /// <summary>
    /// Base class for Sales Rep Commission tests providing common validation
    /// and calculation methods.
    /// </summary>
    [TestClass]
    public abstract class BaseSalesRepCommissionTest : BaseTest
    {
        /// <summary>
        /// Validates commission case reference fields (hospital, surgeon, procedure, etc.).
        /// Patient id and case date are optional parameters.
        /// </summary>
        protected void ValidateCommissionCaseReferences(
            JsonElement commission,
            Guid expectedHospitalId,
            Guid expectedSurgeonId,
            Guid expectedProcedureId,
            string? expectedPatientId,
            DateTime? expectedCaseDate,
            TestContext? testContext = null
        )
        {
            // Validate Case Hospital
            Assert.IsTrue(
                commission.TryGetProperty("_leap_casehospital_value", out var commissionHospital),
                "Commission is missing the Case Hospital relationship."
            );
            Assert.AreEqual(
                expectedHospitalId,
                commissionHospital.GetGuid(),
                "Commission Case Hospital should match the expected Hospital."
            );

            // Validate Case Surgeon
            Assert.IsTrue(
                commission.TryGetProperty("_leap_casesurgeon_value", out var commissionSurgeon),
                "Commission is missing the Case Surgeon relationship."
            );
            Assert.AreEqual(
                expectedSurgeonId,
                commissionSurgeon.GetGuid(),
                "Commission Case Surgeon should match the expected Surgeon."
            );

            // Validate Case Procedure
            Assert.IsTrue(
                commission.TryGetProperty("_leap_caseprocedure_value", out var commissionProcedure),
                "Commission is missing the Case Procedure relationship."
            );
            Assert.AreEqual(
                expectedProcedureId,
                commissionProcedure.GetGuid(),
                "Commission Case Procedure should match the expected Procedure."
            );

            // Validate Case Patient if passed
            if (expectedPatientId != null)
            {
                Assert.IsTrue(
                    commission.TryGetProperty("leap_casepatientid", out var commissionPatient),
                    "Commission is missing the Case Patient field."
                );
                Assert.AreEqual(
                    expectedPatientId,
                    commissionPatient.GetString(),
                    "Commission Case Patient should match the expected Patient."
                );
            }

            // Validate Case Date if passed
            if (expectedCaseDate != null)
            {
                Assert.IsTrue(
                    commission.TryGetProperty("leap_casedatetime", out var commissionCaseDate),
                    "Commission is missing the Case Date field."
                );
                Assert.AreEqual(
                    expectedCaseDate.Value.Date,
                    DateTime.Parse(commissionCaseDate.ToString()).ToUniversalTime().Date,
                    "Commission Case Date should match the expected Case Date."
                );
            }

            // if passed, use testContext to log success
            testContext?.WriteLine("✓ Commission case reference fields validated successfully.");
        }

        /// <summary>
        /// Validates commission rate and amount based on case product configuration.
        /// Expects unit price to be resolved (unit prices, unit price override, io unit price override, etc.)
        /// </summary>
        protected void ValidateCommissionRateAndAmount(
            JsonElement commission,
            decimal expectedRate,
            decimal unitPrice,
            decimal quantity,
            TestContext? testContext = null
        )
        {
            // Validate Commission Rate
            Assert.IsTrue(
                commission.TryGetProperty("leap_commissionratepercent", out var commissionRate),
                "Commission is missing the Commission Rate Percent field."
            );

            Assert.AreEqual(
                expectedRate,
                commissionRate.GetDecimal(),
                "Commission Rate Percent should match the expected rate."
            );

            // Validate Commission Amount
            Assert.IsTrue(
                commission.TryGetProperty("leap_commissionratemoney", out var commissionAmount),
                "Commission is missing the Commission Amount field."
            );

            var extendedPrice = unitPrice * quantity;
            var expectedAmount = CalculateCommissionAmount(expectedRate, extendedPrice);
            Assert.AreEqual(
                expectedAmount,
                commissionAmount.GetDecimal(),
                "Commission Amount should match the expected amount based on the rate and extended price."
            );

            // if passed, use testContext to log success
            testContext?.WriteLine("✓ Commission rate and amount validated successfully.");
        }

        /// <summary>
        /// Validates commission cost margin for Stock & Bill products.
        /// </summary>
        protected void ValidateCommissionCostMargin(
            JsonElement commission,
            decimal unitPrice,
            decimal costPrice,
            TestContext? testContext = null
        )
        {
            Assert.IsTrue(
                commission.TryGetProperty("leap_costmargin", out var commissionCostMargin),
                "Commission is missing the Cost Margin field."
            );
            var expectedCostMargin = CalculateProfitMargin(unitPrice, costPrice);
            Assert.AreEqual(
                expectedCostMargin,
                commissionCostMargin.GetDecimal(),
                "Commission Cost Margin should match the calculated profit margin."
            );

            // if passed, use testContext to log success
            testContext?.WriteLine("✓ Commission cost margin validated successfully.");
        }

        /// <summary>
        /// Validates commission contract and rule relationships.
        /// </summary>
        protected void ValidateCommissionRelationships(
            JsonElement commission,
            Guid expectedContractId,
            Guid expectedRuleId,
            TestContext? testContext = null
        )
        {
            // Validate contract relationship
            Assert.IsTrue(
                commission.TryGetProperty("_leap_salesrepcontract_value", out var contractId),
                "Commission is missing the Sales Rep Contract relationship."
            );
            Assert.AreEqual(
                expectedContractId,
                contractId.GetGuid(),
                "Commission should be linked to the correct Sales Rep Contract."
            );

            // Validate rule relationship
            Assert.IsTrue(
                commission.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId),
                "Commission is missing the Sales Rep Commission Rule relationship."
            );

            Assert.AreEqual(
                expectedRuleId,
                ruleId.GetGuid(),
                "Commission should be linked to the correct Sales Rep Commission Rule."
            );

            // if passed, use testContext to log success
            testContext?.WriteLine("✓ Commission relationships validated successfully.");
        }

        protected void ValidateCommissionOwnership(
            JsonElement commission,
            Guid expectedOwnerId,
            Guid expectedOwningBusinessUnitId,
            Guid? expectedSalesRepId = null,
            Guid? expectedExternalAgentId = null,
            Guid? expectedDistributorId = null,
            TestContext? testContext = null
        )
        {
            // Validate ownership
            Assert.IsTrue(
                commission.TryGetProperty("_ownerid_value", out var commissionOwnerId),
                "Commission is missing the Owner ID field."
            );
            Assert.AreEqual(
                expectedOwnerId,
                commissionOwnerId.GetGuid(),
                "Commission owner ID does not match."
            );

            // Validate owning business unit
            Assert.IsTrue(
                commission.TryGetProperty("_owningbusinessunit_value", out var commissionOwningBusinessUnitId),
                "Commission is missing the Owning Business Unit ID field."
            );
            Assert.AreEqual(
                expectedOwningBusinessUnitId,
                commissionOwningBusinessUnitId.GetGuid(),
                "Commission owning business unit ID does not match."
            );

            // validate sales rep if passed
            if (expectedSalesRepId.HasValue)
            {
                Assert.IsTrue(
                    commission.TryGetProperty("_leap_salesrepid_value", out var commissionSalesRepId),
                    "Commission is missing the Sales Rep ID field."
                );
                Assert.AreEqual(
                    expectedSalesRepId.Value,
                    commissionSalesRepId.GetGuid(),
                    "Commission sales rep ID does not match."
                );
            }

            // validate external agent if passed
            if (expectedExternalAgentId.HasValue)
            {
                Assert.IsTrue(
                    commission.TryGetProperty("_leap_externalagent_value", out var commissionExternalAgentId),
                    "Commission is missing the External Agent ID field."
                );
                Assert.AreEqual(
                    expectedExternalAgentId.Value,
                    commissionExternalAgentId.GetGuid(),
                    "Commission external agent ID does not match."
                );
            }

            // validate distributor if passed
            if (expectedDistributorId.HasValue)
            {
                Assert.IsTrue(
                    commission.TryGetProperty("_leap_distributorid_value", out var commissionDistributorId),
                    "Commission is missing the Distributor ID field."
                );
                Assert.AreEqual(
                    expectedDistributorId.Value,
                    commissionDistributorId.GetGuid(),
                    "Commission distributor ID does not match."
                );
            }

            // if passed, use testContext to log success
            testContext?.WriteLine("✓ Commission ownership validated successfully.");
        }

        /// <summary>
        /// Calculates the profit margin for a case product.
        /// Profit Margin = (Unit Price - Cost Price) / Unit Price
        /// </summary>
        /// <param name="unitPrice">Unit price of the product</param>
        /// <param name="quantity">Quantity sold</param>
        /// <param name="costPrice">Cost price of the product</param>
        /// <returns>The calculated profit margin</returns>
        protected decimal CalculateProfitMargin(decimal unitPrice, decimal costPrice)
        {
            return ((unitPrice - costPrice) * 100) / unitPrice;
        }

        /// <summary>
        /// Calculates the extended price for a case product.
        /// Extended Price = Unit Price × Quantity
        /// </summary>
        /// <param name="unitPrice">Unit price of the product</param>
        /// <param name="quantity">Quantity sold</param>
        /// <returns>The calculated extended price</returns>
        protected decimal CalculateExtendedPrice(decimal unitPrice, decimal quantity)
        {
            return unitPrice * quantity;
        }

        /// <summary>
        /// Resolves the unit price based on overrides and defaults.
        /// </summary>
        /// <param name="unitPriceOverride">Override for the unit price</param>
        /// <param name="ioUnitPriceOverride">Override for the IO unit price</param>
        /// <param name="defaultUnitPrice">Default unit price if no overrides are set</param>
        /// <returns>The resolved unit price</returns>
        protected decimal ResolveUnitPrice(
            decimal defaultUnitPrice,
            decimal? unitPriceOverride,
            decimal? ioUnitPriceOverride
        )
        {
            if (ioUnitPriceOverride.HasValue)
                return ioUnitPriceOverride.Value;
            if (unitPriceOverride.HasValue)
                return unitPriceOverride.Value;
            return defaultUnitPrice;
        }

        /// <summary>
        /// Calculates the commission amount based on rate and base amount.
        /// Commission Amount = (Rate / 100) × Base Amount
        /// </summary>
        /// <param name="rate">Commission rate as percentage (e.g., 7.5 for 7.5%)</param>
        /// <param name="baseAmount">Base amount to calculate commission on</param>
        /// <returns>The calculated commission amount</returns>
        protected decimal CalculateCommissionAmount(decimal rate, decimal baseAmount)
        {
            return (rate / 100m) * baseAmount;
        }

        /// <summary>
        /// Validates RequiredSalesRepMatch logic by comparing OwnerId or OwningBusinessUnitId
        /// between SalesRepCommissionRule and UsageFormDetail.
        /// When RequiredSalesRepMatch is true, at least one of these must match:
        /// - Rule's OwnerId matches UsageFormDetail's OwnerId, OR
        /// - Rule's OwningBusinessUnitId matches UsageFormDetail's OwningBusinessUnitId
        /// </summary>
        protected void ValidateRequiredSalesRepMatch(
            JsonElement rule,
            JsonElement usageFormDetail,
            bool shouldMatch,
            TestContext? testContext = null
        )
        {
            // Extract Rule's OwnerId and OwningBusinessUnitId
            rule.TryGetProperty("_ownerid_value", out var ruleOwnerId);
            rule.TryGetProperty("_owningbusinessunit_value", out var ruleOwningBusinessUnit);

            // Extract UsageFormDetail's OwnerId and OwningBusinessUnitId
            usageFormDetail.TryGetProperty("_ownerid_value", out var detailOwnerId);
            usageFormDetail.TryGetProperty("_owningbusinessunit_value", out var detailOwningBusinessUnit);

            var ruleOwnerIdGuid = ruleOwnerId.GetGuid();
            var ruleOwnerBusinessUnitGuid = ruleOwningBusinessUnit.GetGuid();
            var detailOwnerIdGuid = detailOwnerId.GetGuid();
            var detailOwnerBusinessUnitGuid = detailOwningBusinessUnit.GetGuid();

            // Check if OwnerId matches
            var ownerIdMatches = ruleOwnerIdGuid == detailOwnerIdGuid;

            // Check if OwningBusinessUnitId matches
            var owningBusinessUnitMatches = ruleOwnerBusinessUnitGuid == detailOwnerBusinessUnitGuid;

            if (shouldMatch)
            {
                Assert.IsTrue(
                    ownerIdMatches || owningBusinessUnitMatches,
                    $"RequiredSalesRepMatch should be satisfied. Rule OwnerId: {ruleOwnerIdGuid}, Detail OwnerId: {detailOwnerIdGuid}, " +
                    $"Rule OwningBusinessUnit: {ruleOwnerBusinessUnitGuid}, Detail OwningBusinessUnit: {detailOwnerBusinessUnitGuid}"
                );
                testContext?.WriteLine($"✓ RequiredSalesRepMatch validation passed: OwnerId match={ownerIdMatches}, OwningBusinessUnitId match={owningBusinessUnitMatches}");
            }
            else
            {
                Assert.IsFalse(
                    ownerIdMatches || owningBusinessUnitMatches,
                    $"RequiredSalesRepMatch should NOT be satisfied. Rule OwnerId: {ruleOwnerIdGuid}, Detail OwnerId: {detailOwnerIdGuid}, " +
                    $"Rule OwningBusinessUnit: {ruleOwnerBusinessUnitGuid}, Detail OwningBusinessUnit: {detailOwnerBusinessUnitGuid}"
                );
                testContext?.WriteLine($"✓ RequiredSalesRepMatch validation correctly rejected: OwnerId match={ownerIdMatches}, OwningBusinessUnitId match={owningBusinessUnitMatches}");
            }
        }
    }
}
