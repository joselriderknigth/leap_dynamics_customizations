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
    [TestCategory("VPStandard")]
    public class VPSalesRepCommissionTests : BaseSalesRepCommissionTest
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
        private SystemUserService _systemUsers = null!;
        private TeamService _teams = null!;

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
            _systemUsers = new SystemUserService(Client);
            _teams = new TeamService(Client);
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

            TestContext.WriteLine($"Cleanup completed for test with Correlation ID: {Config.CorrelationId}");
        }

        [TestMethod]
        public async Task VPStandardCommissionHappyPath()
        {
            /**
            * Scenario 1: VP Standard commissions are created
            * 
            * Business Logic:
            * - Managers and VPs get paid a percentage of commission when a product in a specified 
            *   Sales Category sells
            * - VP Standard rule type allows creating commission rules with Sales Category selection
            * - Commissions are calculated as a percentage of total sales after adjustment
            * - Commission is only applied if the case date falls within the rule's start and end dates
            * - For consignment products with supplier commission > 20%, VP commission applies
            * - This commission type always generates regardless of standalone rules
            * 
            * Test validates that a VP Standard sales rep commission is properly created when:
            * - A Case with hospital and physician is established
            * - A Stock & Bill Product and UsageFormDetail are created
            * - A SalesRepContract with Rep Role = VP is configured
            * - A SalesRepCommissionRule with Commission Rule Type = VP Standard is created
            * - The rule's Start/End dates encompass the case date
            * - Case regeneration is triggered
            * 
            * Expected outcome: 
            * - SalesRepCommission is created with correct field values
            * - Sales Category field is properly populated on commission
            * - Commission amount is calculated as percentage of total sales
            */

            /**
            * step 1: case setup
            */
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(
                hospital,
                physician,
                procedure,
                patientId,
                caseDateTime
            );

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            /**
            * step 2: products and case products setup
            */
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;

            // Create a sales rep account
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 100.00m;
            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 200.00m;
            var cpQuantity = 1;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    manufacturer,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            /**
            * step 3: create sales rep contract with VP rep role
            * 
            * The contract specifies commission rate ranges (High/Low) for manufacturers.
            * Rep Role is set to VP to indicate this is a VP-level commission.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 5m;
            var repMaxRate = 10m;
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: repMinRate,
                    maxCommissionRate: repMaxRate,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,  // VP role
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with VP Standard category: {salesRepContractId}");

            /**
            * step 4: create VP Standard commission rule
            *
            * This rule defines when the VP commission is paid:
            * - Commission Rule Type = VP Standard (option 1)
            * - Sales Category selection to filter which products qualify
            * - Commission Split = percentage of total sales to allocate
            * - Start/End dates determine when rule is active
            * - Invoice date must fall within this date range for commission to apply
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesCategory = salesCategory, // Specific sales category this rule applies to
                    SalesRepId = Config.OwnerUserId,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            /**
            * step 5: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(2)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 6: validate sales rep commission creation and field values
            */

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                salesRepCommissionRuleId
            );
            Assert.AreEqual(
                salesRepCommissions.Count,
                1,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            // Get the VP Standard commission
            var commission = salesRepCommissions.First(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32()
                        == OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard
                    && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "VP Standard Sales Rep Commission should be created.");

            // Validate SalesCategory field is populated correctly
            commission.TryGetProperty("leap_salescategory", out var commissionCategory);
            Assert.AreEqual(
                salesCategory,
                commissionCategory.GetInt32(),
                "Commission Sales Category should match the rule's specified Sales Category."
            );

            var resolvedUnitPrice = ResolveUnitPrice(
                defaultUnitPrice: cpUnitPrice,
                unitPriceOverride: null,
                ioUnitPriceOverride: null
            );

            // Use base class validation methods with config values
            ValidateCommissionCaseReferences(
                commission: commission,
                expectedHospitalId: hospital,
                expectedSurgeonId: physician,
                expectedProcedureId: procedure,
                expectedPatientId: patientId,
                expectedCaseDate: caseDateTime,
                testContext: TestContext
            );

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: repMaxRate, // VP commission should use the max rate from the contract
                unitPrice: resolvedUnitPrice,
                quantity: cpQuantity,
                testContext: TestContext
            );

            ValidateCommissionCostMargin(
                commission: commission,
                unitPrice: resolvedUnitPrice,
                costPrice: productCostPrice,
                testContext: TestContext
            );

            ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
            );

            var systemUser = await _systemUsers.GetAsync(Config.OwnerUserId);
            systemUser.TryGetProperty("_businessunitid_value", out var businessUnitId);
            ValidateCommissionOwnership(
                commission: commission,
                expectedOwnerId: Config.OwnerUserId,
                expectedOwningBusinessUnitId: businessUnitId.GetGuid(),
                expectedSalesRepId: Config.OwnerUserId,
                expectedExternalAgentId: null,
                expectedDistributorId: null,
                testContext: TestContext
            );

            // Validate anticipated commission was calculated and applied to usage form detail
            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(
                usageFormDetails.Count > 0,
                "There should be at least 1 Usage Form Detail for the Case."
            );

            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty(
                    "leap_anticipatedsalesrepcommission",
                    out var commissionProperty
                );
                var anticipatedCommission = commissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );
            }

            TestContext.WriteLine($"Assertions completed for Case ID: {caseRecord}");
        }

        [TestMethod]
        [TestCategory("Standalone")]
        public async Task VPStandardCommissionIgnoresStandaloneRules()
        {
            /**
            * Scenario 2: VP Standard commissions create even when Standalone rules exist
            * 
            * Business Logic:
            * - VP Standard commission type has priority and always generates regardless of 
            *   other standalone commission rules that may exist
            * - The presence of standalone rules (different type/criteria) should not prevent
            *   VP Standard commission creation
            * - Multiple commission types can coexist for the same transaction
            * 
            * Test validates that VP Standard sales rep commission is correctly created when:
            * - Same setup as Scenario 1 is established
            * - An additional Standalone SalesRepCommissionRule (different rule type) is created
            * - Case regeneration is triggered
            * 
            * Expected outcome: 
            * - VP Standard commission is created with correct field values
            * - Standalone rule exists but does not interfere with or prevent VP Standard creation
            * - Both rule types can coexist on the same case
            * - All field validations match Scenario 1
            */

            /**
            * step 1: case setup
            */
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            /**
            * step 2: products and case products setup
            */
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;

            // Create a sales rep account
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 100.00m;
            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 200.00m;
            var cpQuantity = 1;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    manufacturer,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            /**
            * step 3: create VP sales rep contract for scenario 2
            * 
            * Same as Scenario 1 - establishes commission rate ranges for the VP.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 5m;
            var repMaxRate = 10m;
            var vpStandardContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: repMinRate,
                    maxCommissionRate: repMaxRate,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            Assert.IsNotNull(vpStandardContractId, "VP Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created VP Sales Rep Contract: {vpStandardContractId}");

            /**
            * step 4: create VP Standard commission rule for scenario 2
            * 
            * Same as Scenario 1 - defines VP commission as percentage of adjusted sales.
            */
            var vpRuleStartDate = contractStartDate;
            var vpRuleEndDate = contractEndDate;

            var vpStandardRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: vpRuleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: vpStandardContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = vpRuleEndDate,
                    SalesCategory = salesCategory,
                    OverrideRate = 7.5m,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            Assert.IsNotNull(vpStandardRuleId, "VP Standard Commission Rule should be created.");
            TestContext.WriteLine($"Created VP Standard Commission Rule: {vpStandardRuleId}");

            /**
            * step 5: create Standalone commission rule
            * 
            * This rule tests that VP Standard commissions are not blocked or prevented 
            * by the existence of other standalone rule types.
            * The VP Standard commission type has priority and should always generate
            * regardless of whether standalone rules exist or fire.
            */
            var standaloneRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: vpStandardContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    HospitalId = hospital,
                    OverrideRate = 3.0m,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            Assert.IsNotNull(standaloneRuleId, "Standalone Commission Rule should be created.");
            TestContext.WriteLine($"Created Standalone Commission Rule: {standaloneRuleId}");

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
            * step 7: validate Standalone commission is created correctly
            */

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                standaloneRuleId
            );
            Assert.AreEqual(
                1,
                standaloneCommissions.Count,
                "Exactly 1 Standalone Sales Rep Commission should be created for this Case and Rule."
            );

            /**
            * step 8: validate VP Standard commission is created correctly
            */

            var vpStandardCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                vpStandardRuleId
            );
            Assert.AreEqual(
                1,
                vpStandardCommissions.Count,
                "Exactly 1 VP Standard Sales Rep Commission should be created for this Case and Rule."
            );

            // Find the VP Standard commission
            var commission = vpStandardCommissions.First(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32()
                        == OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard
                    && ruleId.GetGuid() == vpStandardRuleId;
            });

            Assert.IsNotNull(commission, "VP Standard Sales Rep Commission should be created.");

            // Validate SalesCategory field is populated correctly
            commission.TryGetProperty("leap_salescategory", out var commissionCategory);
            Assert.AreEqual(
                salesCategory,
                commissionCategory.GetInt32(),
                "Commission Sales Category should match the rule's specified Sales Category."
            );

            var resolvedUnitPrice = ResolveUnitPrice(
                defaultUnitPrice: cpUnitPrice,
                unitPriceOverride: null,
                ioUnitPriceOverride: null
            );

            // Use base class validation methods with config values
            ValidateCommissionCaseReferences(
                commission: commission,
                expectedHospitalId: hospital,
                expectedSurgeonId: physician,
                expectedProcedureId: procedure,
                expectedPatientId: patientId,
                expectedCaseDate: caseDateTime,
                testContext: TestContext
            );

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: repMaxRate, // VP commission should use the max rate from the contract
                unitPrice: resolvedUnitPrice,
                quantity: cpQuantity,
                testContext: TestContext
            );

            ValidateCommissionCostMargin(
                commission: commission,
                unitPrice: resolvedUnitPrice,
                costPrice: productCostPrice,
                testContext: TestContext
            );

            ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: vpStandardContractId,
                expectedRuleId: vpStandardRuleId,
                testContext: TestContext
            );

            var systemUser = await _systemUsers.GetAsync(Config.OwnerUserId);
            systemUser.TryGetProperty("_businessunitid_value", out var businessUnitId);
            ValidateCommissionOwnership(
                commission: commission,
                expectedOwnerId: Config.OwnerUserId,
                expectedOwningBusinessUnitId: businessUnitId.GetGuid(),
                expectedSalesRepId: Config.OwnerUserId,
                expectedExternalAgentId: null,
                expectedDistributorId: null,
                testContext: TestContext
            );

            // Validate anticipated commission was calculated and applied to usage form detail
            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(
                usageFormDetails.Count > 0,
                "There should be at least 1 Usage Form Detail for the Case."
            );

            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty(
                    "leap_anticipatedsalesrepcommission",
                    out var commissionProperty
                );
                var anticipatedCommission = commissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );
            }

            TestContext.WriteLine($"Assertions completed for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task VPStandardCommission_DifferentSalesCategory_NoCommissions()
        {
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Product setup with Woundcare category
            var manufacturerCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(manufacturerCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            // Case product setup
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Contract setup
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            // Commission rule setup with different sales category (Biologics instead of Woundcare)
            var ruleSalesCategory = OptionSetValues.Account.SalesCategory.Biologics;
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = ruleSalesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule with different category: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate no commissions were created due to category mismatch
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, 
                "No Sales Rep Commission should be created when manufacturer category does not match rule category.");

            TestContext.WriteLine($"✓ Validated no commission created for mismatched sales category.");
        }

        [TestMethod]
        public async Task VPStandardCommission_MinRate_ProfitMargin2999()
        {
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            // Cost and unit price to achieve 29.99% profit margin: (200 - 140.02) / 200 * 100 = 29.99
            var productCostPrice = 140.02m;
            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            var cpUnitPrice = 200.00m;
            var cpQuantity = 1;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Contract setup with min and max rates
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var repMinRate = 5m;
            var repMaxRate = 10m;
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: repMinRate,
                    maxCommissionRate: repMaxRate,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission uses min rate for 29.99% profit margin
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "One Sales Rep Commission should be created.");

            var commission = salesRepCommissions[0];

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: repMinRate,
                unitPrice: cpUnitPrice,
                quantity: cpQuantity,
                testContext: TestContext
            );

            TestContext.WriteLine($"✓ Validated commission uses min rate for 29.99% profit margin.");
        }

        [TestMethod]
        public async Task VPStandardCommission_MaxRate_ProfitMargin30()
        {
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            // Cost and unit price to achieve 30.00% profit margin: (200 - 140) / 200 * 100 = 30.00
            var productCostPrice = 140.00m;
            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            var cpUnitPrice = 200.00m;
            var cpQuantity = 1;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Contract setup with min and max rates
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var repMinRate = 5m;
            var repMaxRate = 10m;
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: repMinRate,
                    maxCommissionRate: repMaxRate,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission uses max rate for 30.00% profit margin
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "One Sales Rep Commission should be created.");

            var commission = salesRepCommissions[0];

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: repMaxRate,
                unitPrice: cpUnitPrice,
                quantity: cpQuantity,
                testContext: TestContext
            );

            TestContext.WriteLine($"✓ Validated commission uses max rate for 30.00% profit margin.");
        }

        [TestMethod]
        public async Task VPStandardCommission_Threshold2001Percent_CommissionGenerated()
        {
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            // Cost and unit price to achieve 20.01% profit margin: (200 - 159.98) / 200 * 100 = 20.01
            var productCostPrice = 159.98m;
            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            var cpUnitPrice = 200.00m;
            var cpQuantity = 1;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Contract setup
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission was created for 20.00% profit margin (meets threshold)
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, 
                "One Sales Rep Commission should be created for 20.00% profit margin (meets threshold).");

            var commission = salesRepCommissions[0];

            ValidateCommissionCostMargin(
                commission: commission,
                unitPrice: cpUnitPrice,
                costPrice: productCostPrice,
                testContext: TestContext
            );

            TestContext.WriteLine($"✓ Validated commission created for 20.00% profit margin.");
        }

        [TestMethod]
        public async Task VPStandardCommission_Threshold1999_NoCommissionGenerated()
        {
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            // Cost and unit price to achieve 19.99% profit margin: (200 - 160.02) / 200 * 100 = 19.99
            var productCostPrice = 160.02m;
            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            var cpUnitPrice = 200.00m;
            var cpQuantity = 1;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Contract setup
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate no commission was created for 19.99% profit margin (below threshold)
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, 
                "No Sales Rep Commission should be created for 19.99% profit margin (below threshold).");

            TestContext.WriteLine($"✓ Validated no commission created for 19.99% profit margin.");
        }

        [TestMethod]
        public async Task VPStandardCommission_CaseDateOnContractStartDate_CommissionGenerated()
        {
            // Case setup with case date equal to contract start date
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var contractStartDate = DateTime.UtcNow.Date.AddDays(5);
            var caseDateTime = contractStartDate; // Case date equals contract start date
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord} on contract start date");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Contract setup
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with start date: {contractStartDate}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, 
                "One Sales Rep Commission should be created when case date equals contract start date.");

            TestContext.WriteLine($"✓ Validated commission created when case date equals contract start date.");
        }

        [TestMethod]
        public async Task VPStandardCommission_CaseDateBeforeContractStartDate_NoCommission()
        {
            // Case setup with case date before contract start date
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var contractStartDate = DateTime.UtcNow.Date.AddDays(5);
            var caseDateTime = contractStartDate.AddDays(-1); // Case date before contract start date
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord} before contract start date");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Contract setup
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with start date: {contractStartDate}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate no commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, 
                "No Sales Rep Commission should be created when case date is before contract start date.");

            TestContext.WriteLine($"✓ Validated no commission created when case date is before contract start date.");
        }

        [TestMethod]
        public async Task VPStandardCommission_CaseDateOnContractEndDate_CommissionGenerated()
        {
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddDays(30);
            var caseDateTime = contractEndDate; // Case date equals contract end date
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord} on contract end date");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Contract setup
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with end date: {contractEndDate}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, 
                "One Sales Rep Commission should be created when case date equals contract end date.");

            TestContext.WriteLine($"✓ Validated commission created when case date equals contract end date.");
        }

        [TestMethod]
        public async Task VPStandardCommission_CaseDateAfterContractEndDate_NoCommission()
        {
            // Case setup with case date after contract end date
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddDays(30);
            var caseDateTime = contractEndDate.AddDays(1); // Case date after contract end date
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord} after contract end date");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Contract setup
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with end date: {contractEndDate}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate no commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, 
                "No Sales Rep Commission should be created when case date is after contract end date.");

            TestContext.WriteLine($"✓ Validated no commission created when case date is after contract end date.");
        }

        [TestMethod]
        public async Task VPStandardCommission_NoContractEndDate_CommissionGenerated()
        {
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow.AddYears(10); // Far future date
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord} with far future date");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Contract setup without end date
            var contractStartDate = DateTime.UtcNow.Date;
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"VP Rep Contract {Config.CorrelationId}",
                    EndDate = null, // No end date
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with no end date");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = null, // No end date
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, 
                "One Sales Rep Commission should be created when contract has no end date.");

            TestContext.WriteLine($"✓ Validated commission created when contract has no end date.");
        }

        [TestMethod]
        public async Task VPStandardCommission_ExternalAgent_CorrectPayee()
        {
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // External agent setup - using Config.OwnerUserId as external agent
            var externalAgentId = Config.OwnerUserId;

            // Contract setup for external agent
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.ExternalSalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"External Agent Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    ExternalAgentId = externalAgentId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created External Agent Contract: {salesRepContractId}");

            // Commission rule setup for external agent
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    ExternalAgentId = externalAgentId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission with correct external agent payee
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "One Sales Rep Commission should be created.");

            var commission = salesRepCommissions[0];

            var systemUser = await _systemUsers.GetAsync(Config.OwnerUserId);
            systemUser.TryGetProperty("_businessunitid_value", out var businessUnitId);

            ValidateCommissionOwnership(
                commission: commission,
                expectedOwnerId: Config.OwnerUserId,
                expectedOwningBusinessUnitId: businessUnitId.GetGuid(),
                expectedSalesRepId: null,
                expectedExternalAgentId: externalAgentId,
                expectedDistributorId: null,
                testContext: TestContext
            );

            TestContext.WriteLine($"✓ Validated commission payee is external agent.");
        }

        [TestMethod]
        public async Task VPStandardCommission_SalesRep_CorrectPayee()
        {
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Sales rep setup - using Config.OwnerUserId as sales rep
            var salesRepId = Config.OwnerUserId;

            // Contract setup for sales rep
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"Sales Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = salesRepId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            // Commission rule setup for sales rep
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = salesRepId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission with correct sales rep payee
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "One Sales Rep Commission should be created.");

            var commission = salesRepCommissions[0];

            var systemUser = await _systemUsers.GetAsync(Config.OwnerUserId);
            systemUser.TryGetProperty("_businessunitid_value", out var businessUnitId);

            ValidateCommissionOwnership(
                commission: commission,
                expectedOwnerId: Config.OwnerUserId,
                expectedOwningBusinessUnitId: businessUnitId.GetGuid(),
                expectedSalesRepId: salesRepId,
                expectedExternalAgentId: null,
                expectedDistributorId: null,
                testContext: TestContext
            );

            TestContext.WriteLine($"✓ Validated commission payee is sales rep.");
        }

        [TestMethod]
        public async Task VPStandardCommission_Distributor_CorrectPayee()
        {
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Product setup
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Distributor setup - create a distributor account
            var distributorId = await _accounts.SetupDistributorAsync();

            // Get team and team business unit
            var team = await _teams.GetAsync(Seed.TeamId);
            team.TryGetProperty("_businessunitid_value", out var businessUnitId);

            Assert.IsNotNull(businessUnitId, "Team should have a business unit.");

            // Contract setup for distributor
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.Distributor,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Seed.TeamId
                ) with
                {
                    Name = $"Distributor Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    DistributorId = distributorId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                    IsOwnedByTeam = true,
                }
            );

            TestContext.WriteLine($"Created Distributor Contract: {salesRepContractId}");

            // Commission rule setup for distributor
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Seed.TeamId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    DistributorId = distributorId,
                    BusinessUnitId = businessUnitId.GetGuid(),
                    IsOwnedByTeam = true,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(2));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission with correct distributor payee
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "One Sales Rep Commission should be created.");

            var commission = salesRepCommissions[0];

            ValidateCommissionOwnership(
                commission: commission,
                expectedOwnerId: Seed.TeamId,
                expectedOwningBusinessUnitId: businessUnitId.GetGuid(),
                expectedSalesRepId: null,
                expectedExternalAgentId: null,
                expectedDistributorId: distributorId,
                testContext: TestContext
            );

            TestContext.WriteLine($"✓ Validated commission payee is distributor.");
        }
    }
}
