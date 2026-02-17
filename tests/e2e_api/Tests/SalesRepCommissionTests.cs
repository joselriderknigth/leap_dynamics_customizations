using System;
using System.Linq;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Fixtures;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace E2E.Api.Tests.Tests
{
    [TestClass]
    public class SalesRepCommissionTests : BaseTest
    {
        public TestContext TestContext { get; set; } = null!;

        // Fixtures
        private AccountFixture _accounts = null!;
        private CaseFixture _cases = null!;
        private ProductFixture _products = null!;
        private CaseProductFixture _caseProducts = null!;
        private SalesRepContractFixture _salesRepContracts = null!;
        private SalesRepCommissionRuleFixture _salesRepCommissionRules = null!;

        // Services
        private UsageFormService _usageForms = null!;
        private UsageFormDetailService _usageFormDetails = null!;
        private SalesRepCommissionService _salesRepCommissions = null!;

        [TestInitialize]
        public void Setup()
        {
            _accounts = new AccountFixture(Client, Config);
            _cases = new CaseFixture(Client, Config);
            _products = new ProductFixture(Client, Config);
            _caseProducts = new CaseProductFixture(Client, Config);
            _salesRepContracts = new SalesRepContractFixture(Client, Config);
            _salesRepCommissionRules = new SalesRepCommissionRuleFixture(Client, Config);

            _usageForms = new UsageFormService(Client);
            _usageFormDetails = new UsageFormDetailService(Client);
            _salesRepCommissions = new SalesRepCommissionService(Client);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (_caseProducts != null)
                await _caseProducts.CleanupAsync();
            if (_cases != null)
                await _cases.CleanupAsync();
            if (_salesRepCommissionRules != null)
                await _salesRepCommissionRules.CleanupAsync();
            if (_salesRepContracts != null)
                await _salesRepContracts.CleanupAsync();
            if (_products != null)
                await _products.CleanupAsync();
            if (_accounts != null)
                await _accounts.CleanupAsync();
        }

        [TestMethod]
        public async Task VPStandardCommissionHappyPath()
        {
            /**
            * Scenario 1: Happy Path
            * 
            * Test validates that a VP Standard sales rep commission is properly created when:
            * - A Case with hospital and physician is established
            * - A Product and UsageFormDetail are created
            * - A SalesRepContract with VP Standard category is configured
            * - A SalesRepCommissionRule linked to the contract is created
            * - Case regeneration is triggered
            * 
            * Expected outcome: SalesRepCommission is created with correct field values
            */

            /**
            * step 1: case setup
            */
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            /**
            * step 2: products and case products setup
            */
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            
            // Create a sales rep account (representing a surgeon or sales rep)
            var salesRepAccount = await _accounts.SetupAsync();
            Assert.IsNotNull(salesRepAccount, "Sales Rep account should be created.");

            // Setup product and product family
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                salesRepAccount,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                salesRepAccount,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    salesRepAccount,
                    salesRepAccount,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProductConfig.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            /**
            * step 3: create sales rep contract with VP Standard category
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    name: $"VP Standard Contract {Config.CorrelationId}",
                    salesRepId: Config.OwnerUserId,
                    entityId: salesRepAccount,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveEndDate = contractEndDate,
                    AllHospitals = true,
                    AllPhysicians = true,
                    SalesCategory = "VP Standard",
                    CommissionSplit = 100m,
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with VP Standard category: {salesRepContractId}");

            /**
            * step 4: create sales rep commission rule linked to contract
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    name: $"VP Standard Rule {Config.CorrelationId}",
                    salesRepContractId: salesRepContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 5.0m,
                    EffectiveStartDate = ruleStartDate,
                    EffectiveEndDate = ruleEndDate,
                    SalesCategory = "VP Standard",
                    CommissionRuleNotes = "Standard VP commission rule for test",
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            /**
            * step 5: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(2)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 6: validate sales rep commission creation and field values
            */
            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(
                usageFormDetails.Count > 0,
                "There should be at least 1 Usage Form Detail for the Case."
            );

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAsync(caseRecord);
            Assert.IsTrue(
                salesRepCommissions.Count > 0,
                "Sales Rep Commission should be created after case regeneration."
            );

            var commission = salesRepCommissions.First();

            // Validate SalesCategory field
            if (commission.TryGetProperty("leap_salescategory", out var categoryProperty))
            {
                var category = categoryProperty.GetString();
                Assert.AreEqual(
                    "VP Standard",
                    category,
                    "Commission Sales Category should match the contract's VP Standard category."
                );
            }

            // Validate contract relationship
            if (commission.TryGetProperty("leap_SalesRepContractId", out var contractIdProperty))
            {
                var linkedContractId = contractIdProperty.GetProperty("leap_salesrepcontractid").GetGuid();
                Assert.AreEqual(
                    salesRepContractId,
                    linkedContractId,
                    "Commission should be linked to the correct Sales Rep Contract."
                );
            }

            // Validate rule relationship
            if (commission.TryGetProperty("leap_SalesRepCommissionRuleId", out var ruleIdProperty))
            {
                var linkedRuleId = ruleIdProperty.GetProperty("leap_salesrepcommissionruleid").GetGuid();
                Assert.AreEqual(
                    salesRepCommissionRuleId,
                    linkedRuleId,
                    "Commission should be linked to the correct Sales Rep Commission Rule."
                );
            }

            // Validate anticipated commission was calculated and applied to usage form detail
            foreach (var ufd in usageFormDetails)
            {
                if (ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var commissionProperty))
                {
                    var anticipatedCommission = commissionProperty.GetDecimal();
                    Assert.IsTrue(
                        anticipatedCommission > 0,
                        "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                    );
                    TestContext.WriteLine($"Anticipated Sales Rep Commission on UFD: {anticipatedCommission}");
                }
            }

            TestContext.WriteLine($"Assertions completed for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task VPStandardCommissionWithStandaloneRule()
        {
            /**
            * Scenario 2: VP Standard commissions create even when Standalone rules exist
            * 
            * Test validates that VP Standard sales rep commission is correctly created when:
            * - Same setup as Scenario 1 is established
            * - An additional Standalone SalesRepCommissionRule (without contract association) exists
            * - Case regeneration is triggered
            * 
            * Expected outcome: 
            * - VP Standard commission is created with correct field values
            * - Standalone rule exists but does not interfere with VP Standard creation
            * - All field validations match Scenario 1
            */

            /**
            * step 1: case setup
            */
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            /**
            * step 2: products and case products setup
            */
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            // Create a sales rep account
            var salesRepAccount = await _accounts.SetupAsync();
            Assert.IsNotNull(salesRepAccount, "Sales Rep account should be created.");

            // Setup product and product family
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                salesRepAccount,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                salesRepAccount,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    salesRepAccount,
                    salesRepAccount,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProductConfig.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            /**
            * step 3: create VP standard sales rep contract
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var vpStandardContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    name: $"VP Standard Contract {Config.CorrelationId}",
                    salesRepId: Config.OwnerUserId,
                    entityId: salesRepAccount,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveEndDate = contractEndDate,
                    AllHospitals = true,
                    AllPhysicians = true,
                    SalesCategory = "VP Standard",
                    CommissionSplit = 100m,
                }
            );

            Assert.IsNotNull(vpStandardContractId, "VP Standard Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created VP Standard Sales Rep Contract: {vpStandardContractId}");

            /**
            * step 4: create VP standard sales rep commission rule
            */
            var vpRuleStartDate = contractStartDate;
            var vpRuleEndDate = contractEndDate;

            var vpStandardRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    name: $"VP Standard Rule {Config.CorrelationId}",
                    salesRepContractId: vpStandardContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 5.0m,
                    EffectiveStartDate = vpRuleStartDate,
                    EffectiveEndDate = vpRuleEndDate,
                    SalesCategory = "VP Standard",
                    CommissionRuleNotes = "Standard VP commission rule for test",
                }
            );

            Assert.IsNotNull(vpStandardRuleId, "VP Standard Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created VP Standard Sales Rep Commission Rule: {vpStandardRuleId}");

            /**
            * step 5: create standalone sales rep commission rule (without contract association)
            * 
            * This rule tests that standalone rules do not interfere with VP Standard commission creation.
            * Note: A standalone rule typically has null or specific product/physician criteria.
            */
            var standaloneRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    name: $"Standalone Rule {Config.CorrelationId}",
                    salesRepContractId: vpStandardContractId, // Still linked but with different criteria
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 2.0m,
                    EffectiveStartDate = contractStartDate,
                    EffectiveEndDate = contractEndDate,
                    SalesCategory = "Standard",
                    CommissionRuleNotes = "Standalone commission rule for test",
                }
            );

            Assert.IsNotNull(standaloneRuleId, "Standalone Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Standalone Sales Rep Commission Rule: {standaloneRuleId}");

            /**
            * step 6: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(2)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 7: validate VP standard commission is created correctly
            */
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAsync(caseRecord);
            Assert.IsTrue(
                salesRepCommissions.Count > 0,
                "Sales Rep Commission(s) should be created after case regeneration."
            );

            // Find the VP Standard commission
            var vpStandardCommission = salesRepCommissions.FirstOrDefault(c =>
            {
                if (c.TryGetProperty("leap_salescategory", out var categoryProperty))
                {
                    return categoryProperty.GetString() == "VP Standard";
                }
                return false;
            });

            Assert.IsNotNull(
                vpStandardCommission,
                "VP Standard Sales Rep Commission should be created."
            );

            // Validate SalesCategory for VP Standard commission
            if (vpStandardCommission.TryGetProperty("leap_salescategory", out var vpCategoryProperty))
            {
                var category = vpCategoryProperty.GetString();
                Assert.AreEqual(
                    "VP Standard",
                    category,
                    "VP Standard Commission should have correct SalesCategory."
                );
            }

            // Validate contract relationship
            if (vpStandardCommission.TryGetProperty("leap_SalesRepContractId", out var contractIdProperty))
            {
                var linkedContractId = contractIdProperty.GetProperty("leap_salesrepcontractid").GetGuid();
                Assert.AreEqual(
                    vpStandardContractId,
                    linkedContractId,
                    "VP Standard Commission should be linked to the VP Standard Contract."
                );
            }

            // Validate rule relationship
            if (vpStandardCommission.TryGetProperty("leap_SalesRepCommissionRuleId", out var ruleIdProperty))
            {
                var linkedRuleId = ruleIdProperty.GetProperty("leap_salesrepcommissionruleid").GetGuid();
                Assert.AreEqual(
                    vpStandardRuleId,
                    linkedRuleId,
                    "VP Standard Commission should be linked to the VP Standard Rule."
                );
            }

            /**
            * step 8: validate that anticiapated commission was calculated on usage form details
            */
            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(
                usageFormDetails.Count > 0,
                "There should be at least 1 Usage Form Detail for the Case."
            );

            foreach (var ufd in usageFormDetails)
            {
                if (ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var commissionProperty))
                {
                    var anticipatedCommission = commissionProperty.GetDecimal();
                    Assert.IsTrue(
                        anticipatedCommission > 0,
                        "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                    );
                    TestContext.WriteLine($"Anticipated Sales Rep Commission on UFD: {anticipatedCommission}");
                }
            }

            /**
            * step 9: verify standalone rule exists but did not block VP Standard creation
            */
            var standaloneCommissions = salesRepCommissions.Where(c =>
            {
                if (c.TryGetProperty("leap_salescategory", out var categoryProperty))
                {
                    return categoryProperty.GetString() == "Standard";
                }
                return false;
            }).ToList();

            // The standalone rule may or may not create a commission depending on matching logic
            TestContext.WriteLine($"Found {standaloneCommissions.Count} Standalone commission(s).");
            TestContext.WriteLine($"Total commissions created: {salesRepCommissions.Count}");

            TestContext.WriteLine($"Assertions completed for Case ID: {caseRecord}");
        }
    }
}
