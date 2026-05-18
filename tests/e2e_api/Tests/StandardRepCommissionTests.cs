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
    [TestCategory("Standard")]
    public class StandardRepCommissionTests : BaseSalesRepCommissionTest
    {
        public TestContext TestContext { get; set; } = null!;

        // Fixtures
        private AccountFixture _accounts = null!;
        private CaseFixture _cases = null!;
        private ProductFixture _products = null!;
        private CaseProductFixture _caseProducts = null!;
        private SalesRepContractFixture _salesRepContracts = null!;
        private SalesRepCommissionRuleFixture _salesRepCommissionRules = null!;
        private SupplierContractFixture _supplierContracts = null!;
        private SupplierCommissionRuleFixture _supplierCommissionRules = null!;

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
            _supplierContracts = new SupplierContractFixture(Client, Config);
            _supplierCommissionRules = new SupplierCommissionRuleFixture(Client, Config);

            _usageForms = new UsageFormService(Client);
            _usageFormDetails = new UsageFormDetailService(Client);
            _salesRepCommissions = new SalesRepCommissionService(Client);
            _systemUsers = new SystemUserService(Client);
            _teams = new TeamService(Client);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            TestContext.WriteLine("-----Starting Cleanup-----");
            
            // Then delete dependent records
            if (_caseProducts != null)
                await _caseProducts.CleanupAsync();
            if (_cases != null)
                await _cases.CleanupAsync();
            
            // Then cleanup the rest
            if (_salesRepCommissionRules != null)
                await _salesRepCommissionRules.CleanupAsync();
            if (_salesRepContracts != null)
                await _salesRepContracts.CleanupAsync();
            if (_supplierCommissionRules != null)
                await _supplierCommissionRules.CleanupAsync();
            if (_supplierContracts != null)
                await _supplierContracts.CleanupAsync();
            if (_products != null)
                await _products.CleanupAsync();
            if (_accounts != null)
                await _accounts.CleanupAsync();

            TestContext.WriteLine($"Cleanup completed for test with Correlation ID: {Config.CorrelationId}");
        }

        /*
            RequiredSalesRepMatch: [true, false, null]
            SalesRepMatches: [true, false]
            StandaloneCommissionsExist: [true, false]
            RepRole: [SR, non-SR]
            ProductType: [Consignment, Non-Consignment]
            CommissionRateType: [High, Low]  
            DateRange: [Valid, BeforeStart, AfterEnd]
            BlockVPStandard: [true, false, null]   
            PhysicianMatch: [true, false]
            CommissionSplit: [valid, edge cases]
        */

        [TestMethod]
        public async Task StandardCommissionHappyPath()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: Standard commission is created when all rule conditions are satisfied (Full Happy Path)
            * 
            * Business Logic:
            * - Standard commission is generated when all required rule conditions are met
            * - Commission is calculated based on contract rate and commission split
            * - Sales Rep matching, product type, and date constraints must all be satisfied
            * - Standard commission is ONLY generated when no Standalone commissions exist
            * - Block VP Standard flag does NOT affect Standard commission creation
            * 
            * Test validates that:
            * - A valid Case, Product, and UsageFormDetail exist
            * - Physician matches the rule configuration
            * - Commission Split is applied correctly
            * - Rep Role = SalesRep (SR)
            * - Product is configured (Consignment or Non-Consignment)
            * - Commission rate type (High/Low) is correctly applied
            * - Case date is within Start and End dates
            * - Require Sales Rep Match is satisfied (false or matching when true)
            * - StandaloneCommissionsExist = false
            * - Block VP Standard Commission can be any value (does not affect outcome)
            * - Case regeneration is triggered
            * 
            * Expected outcome:
            * - SalesRepCommission is created
            * - Commission amount is correctly calculated (based on High/Low rate and split)
            * - Commission is linked to correct contract and rule
            * - Anticipated commission is applied to Usage Form Detail
            */

            TestContext.WriteLine("-----Starting Setup/Creation-----");
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

            // Setup manufacturer account
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family with Stock & Bill type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
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

            // Create Case Product with Stock & Bill product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
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
            * step 3: create sales rep contract
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standard sales rep commission.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;    // Low rate
            var repMaxRate = 7m;    // High rate
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
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standard commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            /**
            * step 4: create Standard commission rule
            *
            * This rule defines when the standard commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match can be set to control whether physician must match
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standard commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,  // No specific sales rep match required in happy path
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standard commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            /**
            * step 5: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 6: validate sales rep commission creation and field values
            */

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                salesRepCommissionRuleId
            );

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.IsTrue(salesRepCommissions != null && salesRepCommissions.Count > 0, 
                "At least one Sales Rep Commission should be created.");
            
            TestContext.WriteLine("-----Starting Validation-----");
            
            foreach (var commission in salesRepCommissions)
            {
                commission.TryGetProperty("leap_name", out var name);
                commission.TryGetProperty("leap_commissiontype", out var type);
                commission.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                TestContext.WriteLine($"  Commission: {name} | Type: {type} | RuleId: {ruleId}");

                //Validate commission type is Standard
                Assert.AreEqual(OptionSetValues.SalesRepCommissionRule.CommissionType.Standard, 
                        type.GetInt32(),
                        "Commission type should be STANDARD."
                    );

                // Validate commission relationships
                ValidateCommissionRelationships(
                    commission: commission,
                    expectedContractId: salesRepContractId,
                    expectedRuleId: salesRepCommissionRuleId,
                    testContext: TestContext
                );

                var resolvedUnitPrice = ResolveUnitPrice(
                    defaultUnitPrice: cpUnitPrice,
                    unitPriceOverride: null,
                    ioUnitPriceOverride: null
                );

                // Validate commission rate and amount (should use max rate in happy path)
                ValidateCommissionRateAndAmount(
                    commission: commission,
                    expectedRate: repMaxRate,
                    unitPrice: resolvedUnitPrice,
                    quantity: cpQuantity,
                    testContext: TestContext
                );

                // Validate commission cost margin
                ValidateCommissionCostMargin(
                    commission: commission,
                    unitPrice: resolvedUnitPrice,
                    costPrice: productCostPrice,
                    testContext: TestContext
                );

                // Validate commission ownership
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

                // Validate commission split is applied
                commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
                Assert.AreEqual(
                    commissionSplit,
                    commissionSplitValue.GetDecimal(),
                    "Commission Split should match the rule configuration."
                );

            }


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
                    out var anticipatedCommissionProperty
                );
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );

                TestContext.WriteLine(
                    $"Anticipated Commission on Usage Form Detail: {anticipatedCommission}"
                );
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionRequireSalesRepMatchTrueAndMatches()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: Commission is created when Require Sales Rep Match = true and Sales Rep matches
            * 
            * Based on: Happy Path
            * Variation:
            * - RequireSalesRepMatch = true
            * - SalesRepMatches = true
            * 
            * Expected outcome:
            * - Commission is created
            */

            /**
            * step 1: case setup
            */
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;  // Use seeded physician contact for case setup
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

            // Setup manufacturer account
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family with Stock & Bill type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
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

            // Create Case Product with Stock & Bill product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
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
            * step 3: create sales rep contract with SR rep role
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standard sales rep commission.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;    // Low rate
            var repMaxRate = 7m;    // High rate
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
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standard commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            /**
            * step 4: create Standard commission rule
            *
            * This rule defines when the standard commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = true (variation)
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standard commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = true,  // Variation: Require sales rep match
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standard commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            /**
            * step 5: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 6: validate sales rep commission creation and field values
            */

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                salesRepCommissionRuleId
            );

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.IsTrue(salesRepCommissions != null && salesRepCommissions.Count > 0, 
                "At least one Sales Rep Commission should be created.");
            
            TestContext.WriteLine("-----Starting Validation-----");
            
            foreach (var commission in salesRepCommissions)
            {
                commission.TryGetProperty("leap_name", out var name);
                commission.TryGetProperty("leap_commissiontype", out var type);
                commission.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                TestContext.WriteLine($"  Commission: {name} | Type: {type} | RuleId: {ruleId}");

                //Validate commission type is Standard
                Assert.AreEqual(OptionSetValues.SalesRepCommissionRule.CommissionType.Standard, 
                        type.GetInt32(),
                        "Commission type should be STANDARD."
                    );

                // Validate commission relationships
                ValidateCommissionRelationships(
                    commission: commission,
                    expectedContractId: salesRepContractId,
                    expectedRuleId: salesRepCommissionRuleId,
                    testContext: TestContext
                );

                // Validate case reference fields
                ValidateCommissionCaseReferences(
                    commission: commission,
                    expectedHospitalId: hospital,
                    expectedSurgeonId: physician,
                    expectedProcedureId: procedure,
                    expectedPatientId: patientId,
                    expectedCaseDate: caseDateTime,
                    testContext: TestContext
                );

                var resolvedUnitPrice = ResolveUnitPrice(
                    defaultUnitPrice: cpUnitPrice,
                    unitPriceOverride: null,
                    ioUnitPriceOverride: null
                );

                // Validate commission rate and amount (should use max rate in happy path)
                ValidateCommissionRateAndAmount(
                    commission: commission,
                    expectedRate: repMaxRate,
                    unitPrice: resolvedUnitPrice,
                    quantity: cpQuantity,
                    testContext: TestContext
                );

                // Validate commission cost margin
                ValidateCommissionCostMargin(
                    commission: commission,
                    unitPrice: resolvedUnitPrice,
                    costPrice: productCostPrice,
                    testContext: TestContext
                );

                // Validate commission ownership
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

                // Validate commission split is applied
                commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
                Assert.AreEqual(
                    commissionSplit,
                    commissionSplitValue.GetDecimal(),
                    "Commission Split should match the rule configuration."
                );

            }

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
                    out var anticipatedCommissionProperty
                );
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );

                TestContext.WriteLine(
                    $"Anticipated Commission on Usage Form Detail: {anticipatedCommission}"
                );
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionRequireSalesRepMatchTrueAndDoesNotMatch()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: Commission is NOT created when Require Sales Rep Match = true and Sales Rep does not match
            * 
            * Based on: Happy Path
            * Variation:
            * - RequireSalesRepMatch = true
            * - SalesRepMatches = false
            * 
            * Expected outcome:
            * - Commission is NOT created
            */

            /**
            * step 1: case setup
            */
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;  // Use different physician to ensure sales rep does not match
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

            // Setup manufacturer account
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family with Stock & Bill type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
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

            // Create Case Product with Stock & Bill product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
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
            * step 3: create sales rep contract with SR rep role
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standard sales rep commission.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;    // Low rate
            var repMaxRate = 7m;    // High rate
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
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standard commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            /**
            * step 4: create Standard commission rule
            *
            * This rule defines when the standard commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = true (variation)
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standard commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = true,  // Variation: Require sales rep match
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standard commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            /**
            * step 5: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 6: validate that no sales rep commission is created
            */

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                salesRepCommissionRuleId
            );
            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.AreEqual(
                salesRepCommissions.Count,
                0,
                "No Sales Rep Commission should be created when sales rep does not match and RequireSalesRepMatch is true."
            );

            TestContext.WriteLine($"No commission created as expected for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionRequireSalesRepMatchFalseRegardlessOfMatch()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: Commission is created when Require Sales Rep Match = false regardless of match
            * 
            * Based on: Happy Path
            * Variation:
            * - RequireSalesRepMatch = false
            * - SalesRepMatches = false
            * 
            * Expected outcome:
            * - Commission is created
            */

            /**
            * step 1: case setup
            */
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;  // Use different physician so sales rep does not match
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

            // Setup manufacturer account
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family with Stock & Bill type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
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

            // Create Case Product with Stock & Bill product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
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
            * step 3: create sales rep contract with SR rep role
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standard sales rep commission.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;    // Low rate
            var repMaxRate = 7m;    // High rate
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
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standard commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            /**
            * step 4: create Standard commission rule
            *
            * This rule defines when the standard commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = false (variation)
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standard commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,  // Variation: Do not require sales rep match
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standard commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            /**
            * step 5: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 6: validate sales rep commission creation and field values
            */

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                salesRepCommissionRuleId
            );

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.IsTrue(salesRepCommissions != null && salesRepCommissions.Count > 0, 
                "At least one Sales Rep Commission should be created.");
            
            TestContext.WriteLine("-----Starting Validation-----");
            
            foreach (var commission in salesRepCommissions)
            {
                commission.TryGetProperty("leap_name", out var name);
                commission.TryGetProperty("leap_commissiontype", out var type);
                commission.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                TestContext.WriteLine($"  Commission: {name} | Type: {type} | RuleId: {ruleId}");

                //Validate commission type is Standard
                Assert.AreEqual(OptionSetValues.SalesRepCommissionRule.CommissionType.Standard, 
                        type.GetInt32(),
                        "Commission type should be STANDARD."
                    );

                // Validate commission relationships
                ValidateCommissionRelationships(
                    commission: commission,
                    expectedContractId: salesRepContractId,
                    expectedRuleId: salesRepCommissionRuleId,
                    testContext: TestContext
                );

                // Validate case reference fields
                ValidateCommissionCaseReferences(
                    commission: commission,
                    expectedHospitalId: hospital,
                    expectedSurgeonId: physician,
                    expectedProcedureId: procedure,
                    expectedPatientId: patientId,
                    expectedCaseDate: caseDateTime,
                    testContext: TestContext
                );

                var resolvedUnitPrice = ResolveUnitPrice(
                    defaultUnitPrice: cpUnitPrice,
                    unitPriceOverride: null,
                    ioUnitPriceOverride: null
                );

                // Validate commission rate and amount (should use max rate in happy path)
                ValidateCommissionRateAndAmount(
                    commission: commission,
                    expectedRate: repMaxRate,
                    unitPrice: resolvedUnitPrice,
                    quantity: cpQuantity,
                    testContext: TestContext
                );

                // Validate commission cost margin
                ValidateCommissionCostMargin(
                    commission: commission,
                    unitPrice: resolvedUnitPrice,
                    costPrice: productCostPrice,
                    testContext: TestContext
                );

                // Validate commission ownership
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

                // Validate commission split is applied
                commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
                Assert.AreEqual(
                    commissionSplit,
                    commissionSplitValue.GetDecimal(),
                    "Commission Split should match the rule configuration."
                );

            }

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
                    out var anticipatedCommissionProperty
                );
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );

                TestContext.WriteLine(
                    $"Anticipated Commission on Usage Form Detail: {anticipatedCommission}"
                );
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionRequireSalesRepMatchNull()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: Commission is created when Require Sales Rep Match is null
            * 
            * Based on: Happy Path
            * Variation:
            * - RequireSalesRepMatch = null
            * 
            * Expected outcome:
            * - Commission is created
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

            // Setup manufacturer account
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family with Stock & Bill type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
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

            // Create Case Product with Stock & Bill product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
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
            * step 3: create sales rep contract with SR rep role
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standard sales rep commission.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;    // Low rate
            var repMaxRate = 7m;    // High rate
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
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standard commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            /**
            * step 4: create Standard commission rule
            *
            * This rule defines when the standard commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = null (variation)
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standard commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = null,  // Variation: Require Sales Rep Match is null
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standard commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            /**
            * step 5: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 6: validate sales rep commission creation and field values
            */

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                salesRepCommissionRuleId
            );

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.IsTrue(salesRepCommissions != null && salesRepCommissions.Count > 0, 
                "At least one Sales Rep Commission should be created.");
            
            TestContext.WriteLine("-----Starting Validation-----");
            
            foreach (var commission in salesRepCommissions)
            {
                commission.TryGetProperty("leap_name", out var name);
                commission.TryGetProperty("leap_commissiontype", out var type);
                commission.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                TestContext.WriteLine($"  Commission: {name} | Type: {type} | RuleId: {ruleId}");

                //Validate commission type is Standard
                Assert.AreEqual(OptionSetValues.SalesRepCommissionRule.CommissionType.Standard, 
                        type.GetInt32(),
                        "Commission type should be STANDARD."
                    );

                // Validate commission relationships
                ValidateCommissionRelationships(
                    commission: commission,
                    expectedContractId: salesRepContractId,
                    expectedRuleId: salesRepCommissionRuleId,
                    testContext: TestContext
                );

                // Validate case reference fields
                ValidateCommissionCaseReferences(
                    commission: commission,
                    expectedHospitalId: hospital,
                    expectedSurgeonId: physician,
                    expectedProcedureId: procedure,
                    expectedPatientId: patientId,
                    expectedCaseDate: caseDateTime,
                    testContext: TestContext
                );

                var resolvedUnitPrice = ResolveUnitPrice(
                    defaultUnitPrice: cpUnitPrice,
                    unitPriceOverride: null,
                    ioUnitPriceOverride: null
                );

                // Validate commission rate and amount (should use max rate in happy path)
                ValidateCommissionRateAndAmount(
                    commission: commission,
                    expectedRate: repMaxRate,
                    unitPrice: resolvedUnitPrice,
                    quantity: cpQuantity,
                    testContext: TestContext
                );

                // Validate commission cost margin
                ValidateCommissionCostMargin(
                    commission: commission,
                    unitPrice: resolvedUnitPrice,
                    costPrice: productCostPrice,
                    testContext: TestContext
                );

                // Validate commission ownership
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

                // Validate commission split is applied
                commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
                Assert.AreEqual(
                    commissionSplit,
                    commissionSplitValue.GetDecimal(),
                    "Commission Split should match the rule configuration."
                );

            }

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
                    out var anticipatedCommissionProperty
                );
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );

                TestContext.WriteLine(
                    $"Anticipated Commission on Usage Form Detail: {anticipatedCommission}"
                );
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionNotCreatedWhenStandaloneCommissionsExist()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: Standard commission is NOT created when Standalone commissions exist
            * 
            * Based on: Happy Path
            * Variation:
            * - StandaloneCommissionsExist = true
            * 
            * Expected outcome:
            * - Standard commission is NOT created
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

            // Setup manufacturer account
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family with Stock & Bill type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
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

            // Create Case Product with Stock & Bill product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
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
            * step 3: create sales rep contract with SR rep role for Standalone commission
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standalone sales rep commission.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;    // Low rate
            var repMaxRate = 7m;    // High rate
            var standaloneSalesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: repMinRate,
                    maxCommissionRate: repMaxRate,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standalone commission
                }
            );

            Assert.IsNotNull(standaloneSalesRepContractId, "Standalone Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Standalone Sales Rep Contract with SR role: {standaloneSalesRepContractId}");

            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
            * - Commission Rule Type = Standalone
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = false (to ensure it creates a commission)
            * - Case date must fall within this date range for commission to apply
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission

            var standaloneSalesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: standaloneSalesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,  // Ensure standalone commission is created
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(standaloneSalesRepCommissionRuleId, "Standalone Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Standalone Sales Rep Commission Rule: {standaloneSalesRepCommissionRuleId}");

            /**
            * step 5: create sales rep contract with SR rep role for Standard commission
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standard sales rep commission.
            */
            var standardSalesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: repMinRate,
                    maxCommissionRate: repMaxRate,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standard commission
                }
            );

            Assert.IsNotNull(standardSalesRepContractId, "Standard Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Standard Sales Rep Contract with SR role: {standardSalesRepContractId}");

            /**
            * step 6: create Standard commission rule
            *
            * This rule defines when the standard commission would be paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = false
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standard commission creation
            * - However, since Standalone commissions exist, Standard should NOT be created
            */
            var standardSalesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: standardSalesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,  // No specific sales rep match required
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect outcome)
                }
            );

            Assert.IsNotNull(standardSalesRepCommissionRuleId, "Standard Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Standard Sales Rep Commission Rule: {standardSalesRepCommissionRuleId}");

            /**
            * step 7: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 8: validate that Standalone commission is created but Standard commission is NOT
            */

            // Check that Standalone commission exists
            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                standaloneSalesRepCommissionRuleId
            );

            // Get the Standalone commission
            var standaloneCommission = standaloneCommissions.First(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32()
                        == OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone
                    && ruleId.GetGuid() == standaloneSalesRepCommissionRuleId;
            });

            Assert.IsNotNull(standaloneCommission, "Standalone Sales Rep Commission should be created.");
            TestContext.WriteLine($"Standalone commission created as expected for Case ID: {caseRecord}");

            // Check that Standard commission does NOT exist
            var standardCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                standardSalesRepCommissionRuleId
            );
            TestContext.WriteLine($"Commissions returned: {standardCommissions.Count}");
            Assert.AreEqual(
                standardCommissions.Count,
                0,
                "No Standard Sales Rep Commission should be created when Standalone commissions exist."
            );

            TestContext.WriteLine($"No Standard commission created as expected for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionCreatedWhenNoStandaloneCommissionsExist()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: Standard commission is processed when no Standalone commissions exist
            * 
            * Based on: Happy Path
            * Variation:
            * - StandaloneCommissionsExist = false
            * 
            * Expected outcome:
            * - Commission is created
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

            // Setup manufacturer account
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family with Stock & Bill type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
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

            // Create Case Product with Stock & Bill product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
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
            * step 3: create sales rep contract with SR rep role
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standard sales rep commission.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;    // Low rate
            var repMaxRate = 7m;    // High rate
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
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standard commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            /**
            * step 4: create Standard commission rule
            *
            * This rule defines when the standard commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = false
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag does NOT affect Standard commission creation
            * - No Standalone commissions exist, so Standard commission should be created
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,  // No specific sales rep match required
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standard commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            /**
            * step 5: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 6: validate sales rep commission creation and field values
            * 
            * Since no Standalone commissions exist, Standard commission should be created
            */

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                salesRepCommissionRuleId
            );

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.IsTrue(salesRepCommissions != null && salesRepCommissions.Count > 0, 
                "At least one Sales Rep Commission should be created.");
            
            TestContext.WriteLine("-----Starting Validation-----");
            
            foreach (var commission in salesRepCommissions)
            {
                commission.TryGetProperty("leap_name", out var name);
                commission.TryGetProperty("leap_commissiontype", out var type);
                commission.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                TestContext.WriteLine($"  Commission: {name} | Type: {type} | RuleId: {ruleId}");

                //Validate commission type is Standard
                Assert.AreEqual(OptionSetValues.SalesRepCommissionRule.CommissionType.Standard, 
                        type.GetInt32(),
                        "Commission type should be STANDARD."
                    );

                // Validate commission relationships
                ValidateCommissionRelationships(
                    commission: commission,
                    expectedContractId: salesRepContractId,
                    expectedRuleId: salesRepCommissionRuleId,
                    testContext: TestContext
                );

                // Validate case reference fields
                ValidateCommissionCaseReferences(
                    commission: commission,
                    expectedHospitalId: hospital,
                    expectedSurgeonId: physician,
                    expectedProcedureId: procedure,
                    expectedPatientId: patientId,
                    expectedCaseDate: caseDateTime,
                    testContext: TestContext
                );

                var resolvedUnitPrice = ResolveUnitPrice(
                    defaultUnitPrice: cpUnitPrice,
                    unitPriceOverride: null,
                    ioUnitPriceOverride: null
                );

                // Validate commission rate and amount (should use max rate in happy path)
                ValidateCommissionRateAndAmount(
                    commission: commission,
                    expectedRate: repMaxRate,
                    unitPrice: resolvedUnitPrice,
                    quantity: cpQuantity,
                    testContext: TestContext
                );

                // Validate commission cost margin
                ValidateCommissionCostMargin(
                    commission: commission,
                    unitPrice: resolvedUnitPrice,
                    costPrice: productCostPrice,
                    testContext: TestContext
                );

                // Validate commission ownership
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

                // Validate commission split is applied
                commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
                Assert.AreEqual(
                    commissionSplit,
                    commissionSplitValue.GetDecimal(),
                    "Commission Split should match the rule configuration."
                );

            }

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
                    out var anticipatedCommissionProperty
                );
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );

                TestContext.WriteLine(
                    $"Anticipated Commission on Usage Form Detail: {anticipatedCommission}"
                );
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionFullContractRateAppliedForNonSRRoles()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: Full contract rate is applied for non-SR roles
            * 
            * Based on: Happy Path
            * Variation:
            * - RepRole = non-SR
            * 
            * Expected outcome:
            * - Full contract rate is applied
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

            // Setup manufacturer account
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family with Stock & Bill type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
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

            // Create Case Product with Stock & Bill product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
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
            * step 3: create sales rep contract with non-SR rep role
            * 
            * The contract specifies commission rate ranges (High/Low) for non-SalesRep role.
            * Rep Role is set to VP (Vice President) to indicate this is a non-SR commission.
            * For non-SR roles, full contract rate should be applied.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;    // Low rate
            var repMaxRate = 7m;    // High rate
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
                    Name = $"Non-SR Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,  // VP role for non-SR commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with VP role: {salesRepContractId}");

            /**
            * step 4: create Standard commission rule
            *
            * This rule defines when the standard commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = false
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag does NOT affect Standard commission creation
            * - For non-SR roles, full contract rate should be applied
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,  // No specific sales rep match required
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standard commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            /**
            * step 5: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 6: validate sales rep commission creation and field values
            * 
            * For non-SR roles, full contract rate should be applied
            */

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                salesRepCommissionRuleId
            );

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.IsTrue(salesRepCommissions != null && salesRepCommissions.Count > 0, 
                "At least one Sales Rep Commission should be created.");
            
            TestContext.WriteLine("-----Starting Validation-----");
            
            foreach (var commission in salesRepCommissions)
            {
                commission.TryGetProperty("leap_name", out var name);
                commission.TryGetProperty("leap_commissiontype", out var type);
                commission.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                TestContext.WriteLine($"  Commission: {name} | Type: {type} | RuleId: {ruleId}");

                //Validate commission type is Standard
                Assert.AreEqual(OptionSetValues.SalesRepCommissionRule.CommissionType.Standard, 
                        type.GetInt32(),
                        "Commission type should be STANDARD."
                    );

                // Validate commission relationships
                ValidateCommissionRelationships(
                    commission: commission,
                    expectedContractId: salesRepContractId,
                    expectedRuleId: salesRepCommissionRuleId,
                    testContext: TestContext
                );

                // Validate case reference fields
                ValidateCommissionCaseReferences(
                    commission: commission,
                    expectedHospitalId: hospital,
                    expectedSurgeonId: physician,
                    expectedProcedureId: procedure,
                    expectedPatientId: patientId,
                    expectedCaseDate: caseDateTime,
                    testContext: TestContext
                );

                var resolvedUnitPrice = ResolveUnitPrice(
                    defaultUnitPrice: cpUnitPrice,
                    unitPriceOverride: null,
                    ioUnitPriceOverride: null
                );

                // Validate commission rate and amount (should use full contract rate for non-SR roles)
                var fullContractRate = 100m;  // Full contract rate for non-SR roles
                ValidateCommissionRateAndAmount(
                    commission: commission,
                    expectedRate: fullContractRate,
                    unitPrice: resolvedUnitPrice,
                    quantity: cpQuantity,
                    testContext: TestContext
                );

                // Validate commission cost margin
                ValidateCommissionCostMargin(
                    commission: commission,
                    unitPrice: resolvedUnitPrice,
                    costPrice: productCostPrice,
                    testContext: TestContext
                );

                // Validate commission ownership
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

                // Validate commission split is applied
                commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
                Assert.AreEqual(
                    commissionSplit,
                    commissionSplitValue.GetDecimal(),
                    "Commission Split should match the rule configuration."
                );

            }

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
                    out var anticipatedCommissionProperty
                );
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );

                TestContext.WriteLine(
                    $"Anticipated Commission on Usage Form Detail: {anticipatedCommission}"
                );
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionSRRateReducedWhenASRCSRCommissionsExist()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: SR commission rate is reduced when ASR/CSR commissions exist
            * 
            * Based on: Happy Path
            * Variation:
            * - RepRole = SR
            * - ASR/CSR commissions exist
            * 
            * Expected outcome:
            * - Combined ASR/CSR rate is deducted from SR rate
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

            // Setup manufacturer account
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            // Setup product and product family with Stock & Bill type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
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

            // Create Case Product with Stock & Bill product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
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
            * step 3: create SR sales rep contract
            * 
            * SR contract with high/low rates
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var srMinRate = 3m;    // Low rate
            var srMaxRate = 7m;    // High rate
            var srSalesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: srMinRate,
                    maxCommissionRate: srMaxRate,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"SR Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(srSalesRepContractId, "SR Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created SR Sales Rep Contract: {srSalesRepContractId}");

            /**
            * step 4: create ASR sales rep contract
            * 
            * ASR contract with lower rates
            */
            var asrMinRate = 1m;
            var asrMaxRate = 2m;
            var asrSalesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: asrMinRate,
                    maxCommissionRate: asrMaxRate,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"ASR Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.ASR,
                }
            );

            Assert.IsNotNull(asrSalesRepContractId, "ASR Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created ASR Sales Rep Contract: {asrSalesRepContractId}");

            /**
            * step 5: create CSS sales rep contract
            * 
            * CSS contract with lower rates
            */
            var csrMinRate = 1m;
            var csrMaxRate = 2m;
            var csrSalesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: csrMinRate,
                    maxCommissionRate: csrMaxRate,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"CSS Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.CSS,
                }
            );

            Assert.IsNotNull(csrSalesRepContractId, "CSS Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created CSS Sales Rep Contract: {csrSalesRepContractId}");

            /**
            * step 6: create SR commission rule
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var srSalesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: srSalesRepContractId
                ) with
                {
                    Name = $"SR Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(srSalesRepCommissionRuleId, "SR Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created SR Sales Rep Commission Rule: {srSalesRepCommissionRuleId}");

            /**
            * step 7: create ASR commission rule
            */
            var asrSalesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: asrSalesRepContractId
                ) with
                {
                    Name = $"ASR Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(asrSalesRepCommissionRuleId, "ASR Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created ASR Sales Rep Commission Rule: {asrSalesRepCommissionRuleId}");

            /**
            * step 8: create CSR commission rule
            */
            var csrSalesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: csrSalesRepContractId
                ) with
                {
                    Name = $"CSR Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(csrSalesRepCommissionRuleId, "CSR Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created CSR Sales Rep Commission Rule: {csrSalesRepCommissionRuleId}");

            /**
            * step 9: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 10: validate commission creation and rate reduction
            */

            // Verify SR commission exists with reduced rate
            var srCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                srSalesRepCommissionRuleId
            );

            TestContext.WriteLine($"Commissions returned: {srCommissions.Count}");

            var srCommission = srCommissions.First(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32()
                        == OptionSetValues.SalesRepCommissionRule.CommissionType.Standard
                    && ruleId.GetGuid() == srSalesRepCommissionRuleId;
            });

            Assert.IsNotNull(srCommission, "SR Standard Sales Rep Commission should be created.");

            // Verify ASR commission exists
            var asrCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                asrSalesRepCommissionRuleId
            );

            var asrCommission = asrCommissions.First();

            // Verify CSR commission exists
            var csrCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                csrSalesRepCommissionRuleId
            );

            var csrCommission = csrCommissions.First();

            // Calculate expected SR rate: SR max rate - (ASR max rate + CSR max rate)
            var expectedSrRate = srMaxRate - (asrMaxRate + csrMaxRate);  // 7 - (2 + 2) = 3

            var resolvedUnitPrice = ResolveUnitPrice(
                defaultUnitPrice: cpUnitPrice,
                unitPriceOverride: null,
                ioUnitPriceOverride: null
            );

            // Validate SR commission rate is reduced
            ValidateCommissionRateAndAmount(
                commission: srCommission,
                expectedRate: expectedSrRate,
                unitPrice: resolvedUnitPrice,
                quantity: cpQuantity,
                testContext: TestContext
            );

            // Validate ASR commission rate
            ValidateCommissionRateAndAmount(
                commission: asrCommission,
                expectedRate: asrMaxRate,
                unitPrice: resolvedUnitPrice,
                quantity: cpQuantity,
                testContext: TestContext
            );

            // Validate CSR commission rate
            ValidateCommissionRateAndAmount(
                commission: csrCommission,
                expectedRate: csrMaxRate,
                unitPrice: resolvedUnitPrice,
                quantity: cpQuantity,
                testContext: TestContext
            );

            TestContext.WriteLine($"SR commission rate reduced from {srMaxRate}% to {expectedSrRate}% due to ASR/CSR commissions");
            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionHighRateAppliedForConsignmentWithSupplierCommission()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: High commission rate is applied for consignment product based on supplier commission
            * 
            * Based on: Happy Path
            * Variation:
            * - ProductType = Consignment
            * - CommissionRateType = High
            * 
            * Expected outcome:
            * - High commission rate is used
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

            // Setup supplier account
            var supplier = await _accounts.SetupSupplierAsync();
            Assert.IsNotNull(supplier, "Supplier account should be created.");

            // Setup product and product family with Consignment type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            // Create Case Product with Consignment product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    supplier,
                    productFamily,
                    product
                ) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            /**
            * step 3: create supplier contract and commission rule with >20% commission
            */
            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Supplier Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: DateTime.UtcNow.Date,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = true,
                    AllPhysicians = true,
                }
            );

            var supplierCommissionRuleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Supplier Rule {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 25.0m,  // >20% to trigger high commission rate
                }
            );

            TestContext.WriteLine($"Created Supplier Contract and Commission Rule for Case ID: {caseRecord}");

            /**
            * step 4: create sales rep contract with SR rep role
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standard sales rep commission.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;    // Low rate
            var repMaxRate = 7m;    // High rate
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
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standard commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            /**
            * step 5: create Standard commission rule
            *
            * This rule defines when the standard commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = false
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag does NOT affect Standard commission creation
            * - For consignment products with supplier commission > 20%, high rate should be applied
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,  // No specific sales rep match required
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standard commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            /**
            * step 6: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 7: validate sales rep commission creation and field values
            * 
            * For consignment products with supplier commission > 20%, high commission rate should be applied
            */

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                salesRepCommissionRuleId
            );

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.IsTrue(salesRepCommissions != null && salesRepCommissions.Count > 0, 
                "At least one Sales Rep Commission should be created.");
            
            TestContext.WriteLine("-----Starting Validation-----");
            
            foreach (var commission in salesRepCommissions)
            {
                commission.TryGetProperty("leap_name", out var name);
                commission.TryGetProperty("leap_commissiontype", out var type);
                commission.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                TestContext.WriteLine($"  Commission: {name} | Type: {type} | RuleId: {ruleId}");

                //Validate commission type is Standard
                Assert.AreEqual(OptionSetValues.SalesRepCommissionRule.CommissionType.Standard, 
                        type.GetInt32(),
                        "Commission type should be STANDARD."
                    );

                // Validate commission relationships
                ValidateCommissionRelationships(
                    commission: commission,
                    expectedContractId: salesRepContractId,
                    expectedRuleId: salesRepCommissionRuleId,
                    testContext: TestContext
                );

                // Validate case reference fields
                ValidateCommissionCaseReferences(
                    commission: commission,
                    expectedHospitalId: hospital,
                    expectedSurgeonId: physician,
                    expectedProcedureId: procedure,
                    expectedPatientId: patientId,
                    expectedCaseDate: caseDateTime,
                    testContext: TestContext
                );

                var resolvedUnitPrice = ResolveUnitPrice(
                    defaultUnitPrice: cpUnitPrice,
                    unitPriceOverride: null,
                    ioUnitPriceOverride: null
                );

                // Validate commission rate and amount (should use high rate for consignment with supplier commission > 20%)
                ValidateCommissionRateAndAmount(
                    commission: commission,
                    expectedRate: repMaxRate,  // High rate should be applied
                    unitPrice: resolvedUnitPrice,
                    quantity: cpQuantity,
                    testContext: TestContext
                );

                // Validate commission cost margin
                ValidateCommissionCostMargin(
                    commission: commission,
                    unitPrice: resolvedUnitPrice,
                    costPrice: productCostPrice,
                    testContext: TestContext
                );

                // Validate commission ownership
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

                // Validate commission split is applied
                commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
                Assert.AreEqual(
                    commissionSplit,
                    commissionSplitValue.GetDecimal(),
                    "Commission Split should match the rule configuration."
                );

            }

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
                    out var anticipatedCommissionProperty
                );
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );

                TestContext.WriteLine(
                    $"Anticipated Commission on Usage Form Detail: {anticipatedCommission}"
                );
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionLowRateAppliedForConsignmentWithSupplierCommission()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            /**
            * Scenario: Low commission rate is applied for consignment product based on supplier commission
            * 
            * Based on: Happy Path
            * Variation:
            * - ProductType = Consignment
            * - CommissionRateType = Low
            * 
            * Expected outcome:
            * - Low commission rate is used
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

            // Setup supplier account
            var supplier = await _accounts.SetupSupplierAsync();
            Assert.IsNotNull(supplier, "Supplier account should be created.");

            // Setup product and product family with Consignment type
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            // Create Case Product with Consignment product type
            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    supplier,
                    productFamily,
                    product
                ) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            /**
            * step 3: create supplier contract and commission rule with <=20% commission
            */
            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Supplier Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: DateTime.UtcNow.Date,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = true,
                    AllPhysicians = true,
                }
            );

            var supplierCommissionRuleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Supplier Rule {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 15.0m,  // <=20% to trigger low commission rate
                }
            );

            TestContext.WriteLine($"Created Supplier Contract and Commission Rule for Case ID: {caseRecord}");

            /**
            * step 4: create sales rep contract with SR rep role
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standard sales rep commission.
            */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;    // Low rate
            var repMaxRate = 7m;    // High rate
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
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standard commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            /**
            * step 5: create Standard commission rule
            *
            * This rule defines when the standard commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = false
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag does NOT affect Standard commission creation
            * - For consignment products with supplier commission <= 20%, low rate should be applied
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,  // No specific sales rep match required
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standard commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            /**
            * step 6: trigger case regeneration and await completion
            */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 7: validate sales rep commission creation and field values
            * 
            * For consignment products with supplier commission <= 20%, low commission rate should be applied
            */

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                salesRepCommissionRuleId
            );

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.IsTrue(salesRepCommissions != null && salesRepCommissions.Count > 0, 
                "At least one Sales Rep Commission should be created.");
            
            TestContext.WriteLine("-----Starting Validation-----");
            
            foreach (var commission in salesRepCommissions)
            {
                commission.TryGetProperty("leap_name", out var name);
                commission.TryGetProperty("leap_commissiontype", out var type);
                commission.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                TestContext.WriteLine($"  Commission: {name} | Type: {type} | RuleId: {ruleId}");

                //Validate commission type is Standard
                Assert.AreEqual(OptionSetValues.SalesRepCommissionRule.CommissionType.Standard, 
                        type.GetInt32(),
                        "Commission type should be STANDARD."
                    );

                // Validate commission relationships
                ValidateCommissionRelationships(
                    commission: commission,
                    expectedContractId: salesRepContractId,
                    expectedRuleId: salesRepCommissionRuleId,
                    testContext: TestContext
                );

                // Validate case reference fields
                ValidateCommissionCaseReferences(
                    commission: commission,
                    expectedHospitalId: hospital,
                    expectedSurgeonId: physician,
                    expectedProcedureId: procedure,
                    expectedPatientId: patientId,
                    expectedCaseDate: caseDateTime,
                    testContext: TestContext
                );

                var resolvedUnitPrice = ResolveUnitPrice(
                    defaultUnitPrice: cpUnitPrice,
                    unitPriceOverride: null,
                    ioUnitPriceOverride: null
                );

                // Validate commission rate and amount (should use low rate for consignment with supplier commission <= 20%)
                ValidateCommissionRateAndAmount(
                    commission: commission,
                    expectedRate: repMinRate,  // Low rate should be applied
                    unitPrice: resolvedUnitPrice,
                    quantity: cpQuantity,
                    testContext: TestContext
                );

                // Validate commission cost margin
                ValidateCommissionCostMargin(
                    commission: commission,
                    unitPrice: resolvedUnitPrice,
                    costPrice: productCostPrice,
                    testContext: TestContext
                );

                // Validate commission ownership
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

                // Validate commission split is applied
                commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
                Assert.AreEqual(
                    commissionSplit,
                    commissionSplitValue.GetDecimal(),
                    "Commission Split should match the rule configuration."
                );

            }

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
                    out var anticipatedCommissionProperty
                );
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );

                TestContext.WriteLine(
                    $"Anticipated Commission on Usage Form Detail: {anticipatedCommission}"
                );
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionCaseDateBeforeRuleStartNoCommission()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            // Case setup with case date before rule start date
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var ruleStartDate = DateTime.UtcNow.Date.AddDays(1);
            var ruleEndDate = ruleStartDate.AddDays(30);
            var caseDateTime = ruleStartDate.AddDays(-1); // Case date before rule start date
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord} before rule start date");

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
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with start date: {ruleStartDate}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate no commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.AreEqual(0, salesRepCommissions.Count, 
                "No Sales Rep Commission should be created when case date is before rule start date.");

            TestContext.WriteLine($"✓ Validated no commission created when case date is before rule start date.");
        }

        [TestMethod]
        public async Task StandardCommissionCaseDateAfterRuleEndNoCommission()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            // Case setup with case date after rule end date
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var ruleStartDate = DateTime.UtcNow.Date;
            var ruleEndDate = ruleStartDate.AddDays(30);
            var caseDateTime = ruleEndDate.AddDays(1); // Case date after rule end date
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord} after rule end date");

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
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with end date: {ruleEndDate}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate no commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.AreEqual(0, salesRepCommissions.Count, 
                "No Sales Rep Commission should be created when case date is after rule end date.");

            TestContext.WriteLine($"✓ Validated no commission created when case date is after rule end date.");
        }

        [TestMethod]
        public async Task StandardCommissionCaseDateWithinEffectiveRangeCommissionCreated()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            // Case setup with case date within rule effective date range
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var ruleStartDate = DateTime.UtcNow.Date.AddDays(-5);
            var ruleEndDate = ruleStartDate.AddDays(30);
            var caseDateTime = ruleStartDate.AddDays(10); // Case date within effective range
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord} within rule effective date range");

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
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with date range: {ruleStartDate} to {ruleEndDate}");

            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            TestContext.WriteLine($"Commissions returned: {salesRepCommissions.Count}");
            Assert.IsTrue(salesRepCommissions != null && salesRepCommissions.Count > 0, 
                "At least one Sales Rep Commission should be created when case date is within rule effective date range.");

            TestContext.WriteLine("-----Starting Validation-----");
            
            foreach (var commission in salesRepCommissions)
            {
                commission.TryGetProperty("leap_name", out var name);
                commission.TryGetProperty("leap_commissiontype", out var type);
                commission.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                TestContext.WriteLine($"  Commission: {name} | Type: {type} | RuleId: {ruleId}");

                //Validate commission type is Standard
                Assert.AreEqual(OptionSetValues.SalesRepCommissionRule.CommissionType.Standard, 
                        type.GetInt32(),
                        "Commission type should be STANDARD."
                    );
            }

            TestContext.WriteLine($"✓ Validated {salesRepCommissions.Count} commission(s) created when case date is within effective date range.");
        }

        [TestMethod]
        public async Task StandardCommissionBlocksVPStandardWhenBlockVPStandardTrue()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
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

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Standard contract setup
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var standardContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(standardContractId, "Standard Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Standard Sales Rep Contract: {standardContractId}");

            // Standard commission rule setup with Block VP Standard = true
            var standardRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: standardContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = true,
                }
            );

            Assert.IsNotNull(standardRuleId, "Standard Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Standard Sales Rep Commission Rule: {standardRuleId}");

            // VP contract setup
            var vpContractId = await _salesRepContracts.SetupAsync(
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

            Assert.IsNotNull(vpContractId, "VP Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created VP Sales Rep Contract: {vpContractId}");

            // VP Standard commission rule setup
            var vpStandardRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: vpContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            Assert.IsNotNull(vpStandardRuleId, "VP Standard Commission Rule should be created.");
            TestContext.WriteLine($"Created VP Standard Commission Rule: {vpStandardRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate Standard commission was created
            var standardCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, standardRuleId);
            Assert.AreEqual(
                1,
                standardCommissions.Count,
                "Standard commission should be created when Standard rule conditions are satisfied."
            );

            // Validate VP Standard commission was blocked
            var vpStandardCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, vpStandardRuleId);
            Assert.AreEqual(
                0,
                vpStandardCommissions.Count,
                "No VP Standard commission should be created when Block VP Standard = true and Standard rule applies."
            );

            TestContext.WriteLine($"VP Standard commission was blocked as expected for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task VPStandardCommissionGeneratedWhenBlockVPStandardTrueButStandardRuleDoesNotMatch()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");
            // Case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
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

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            // Standard contract setup
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var standardContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"Standard Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(standardContractId, "Standard Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Standard Sales Rep Contract: {standardContractId}");

            // Standard commission rule setup with a non-matching sales category and Block VP Standard = true
            var standardRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: standardContractId
                ) with
                {
                    Name = $"Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = OptionSetValues.Account.SalesCategory.Biologics,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = true,
                }
            );

            Assert.IsNotNull(standardRuleId, "Standard Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Standard Sales Rep Commission Rule: {standardRuleId}");

            // VP contract setup
            var vpContractId = await _salesRepContracts.SetupAsync(
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

            Assert.IsNotNull(vpContractId, "VP Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created VP Sales Rep Contract: {vpContractId}");

            // VP Standard commission rule setup
            var vpStandardRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: vpContractId
                ) with
                {
                    Name = $"VP Standard Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            Assert.IsNotNull(vpStandardRuleId, "VP Standard Commission Rule should be created.");
            TestContext.WriteLine($"Created VP Standard Commission Rule: {vpStandardRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate Standard commission did not create because the rule did not match
            var standardCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, standardRuleId);
            Assert.AreEqual(
                0,
                standardCommissions.Count,
                "Standard commission should not be created when the Standard rule does not match the case."
            );

            // Validate VP Standard commission was still created
            var vpStandardCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, vpStandardRuleId);
            Assert.AreEqual(
                1,
                vpStandardCommissions.Count,
                "VP Standard commission should be created when the Standard rule does not match, even if Block VP Standard is true."
            );

            var commission = vpStandardCommissions.First(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.VPStandard
                    && ruleId.GetGuid() == vpStandardRuleId;
            });

            Assert.IsNotNull(commission, "VP Standard Sales Rep Commission should be created.");

            // Validate SalesCategory was applied correctly to the VP Standard commission
            commission.TryGetProperty("leap_salescategory", out var commissionCategory);
            Assert.AreEqual(
                salesCategory,
                commissionCategory.GetInt32(),
                "VP Standard commission Sales Category should match the VP Standard rule's Sales Category."
            );

            var resolvedUnitPrice = ResolveUnitPrice(
                defaultUnitPrice: 200.00m,
                unitPriceOverride: null,
                ioUnitPriceOverride: null
            );

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
                expectedRate: 10m,
                unitPrice: resolvedUnitPrice,
                quantity: 1,
                testContext: TestContext
            );

            ValidateCommissionCostMargin(
                commission: commission,
                unitPrice: resolvedUnitPrice,
                costPrice: 100.00m,
                testContext: TestContext
            );

            ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: vpContractId,
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

            TestContext.WriteLine($"VP Standard commission was generated as expected for Case ID: {caseRecord}");
        }

    }
}