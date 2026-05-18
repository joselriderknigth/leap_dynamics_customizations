using System;
using System.Linq;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Fixtures;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
/*test */
namespace E2E.Api.Tests.Tests
{
    [TestClass]
    [TestCategory("Standalone")]
    public class StandaloneRepCommissionTests : BaseSalesRepCommissionTest
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

        /*
            RequireSalesRepMatch: [true, false, null]
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
        public async Task StandaloneCommissionHappyPath()
        {
            /**
            * Scenario: Standalone commission is created when all rule conditions are satisfied (Full Happy Path)
            * 
            * Business Logic:
            * - Standalone commission is generated when all required rule conditions are met
            
            * - Sales Rep matching, product type, and date constraints must all be satisfied            
            * - Block VP Standard flag does NOT affect Standalone commission creation
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
            * - Block VP Standard Commission can be any value (does not affect outcome)
            * - Case regeneration is triggered
            * 
            * Expected outcome:
            * - SalesRepCommission is created
            * - Commission amount is correctly calculated (based on High/Low rate and split)
            * - Commission is linked to correct contract and rule
            * - Anticipated commission is applied to Usage Form Detail
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
            * Rep Role is set to SR (SalesRep) to indicate this is a standalone sales rep commission.
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
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standalone commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with SR role: {salesRepContractId}");

            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
            * - Commission Rule Type = Standalone
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match can be set to control whether physician must match
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standalone commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var overrideRate = 67m; // Override rate to use instead of contract rates (for testing)
            var commissionSplit = 100m;  // Use 100% of commission

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    HospitalId = hospital,
                    OverrideRate = overrideRate,
                    RequiredSalesRepMatch = false,  // No specific sales rep match required in happy path
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standalone commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");

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
            Assert.AreEqual(
                1,
                salesRepCommissions.Count,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            // Get the Standalone commission
            var commission = salesRepCommissions.First(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32()
                        == OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone
                    && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standalone Sales Rep Commission should be created.");

            // Validate commission relationships
            /*ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
            );*/

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
        public async Task StandaloneCommission_CaseDateOnContractStartDate_CommissionGenerated()
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
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    manufacturer,
                    manufacturer,
                    productFamily,
                    product
                ) with
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
                    Name = $"Standalone Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.CSS,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with start date: {contractStartDate}");

            var overrideRate = 67m; 
            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                    OverrideRate = overrideRate,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, 
                "One Sales Rep Commission should be created when case date equals contract start date.");

            TestContext.WriteLine($"✓ Validated commission created when case date equals contract start date.");
        }

        [TestMethod]
        public async Task StandaloneCommission_CaseDateBeforeContractStartDate_NoCommission()
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
                    Name = $"Standalone Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.ASR,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with start date: {contractStartDate}");

            var overrideRate = 67m; 
            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone,
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
                    OverrideRate = overrideRate,

                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate no commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, 
                "No Sales Rep Commission should be created when case date is before contract start date.");

            TestContext.WriteLine($"✓ Validated no commission created when case date is before contract start date.");
        }

        [TestMethod]
        public async Task StandaloneCommission_CaseDateOnContractEndDate_CommissionGenerated()
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
                    Name = $"Standalone Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.ASR,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with end date: {contractEndDate}");

            var overrideRate = 67m; 
            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone,
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
                    OverrideRate = overrideRate,

                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, 
                "One Sales Rep Commission should be created when case date equals contract end date.");

            TestContext.WriteLine($"✓ Validated commission created when case date equals contract end date.");
        }

          [TestMethod]
        public async Task StandaloneCommission_CaseDateAfterContractEndDate_NoCommission()
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
                    Name = $"Standalone Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            TestContext.WriteLine($"Created Sales Rep Contract with end date: {contractEndDate}");

            var overrideRate = 67m; 
            // Commission rule setup
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = Config.OwnerUserId,
                    OverrideRate = overrideRate,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule: {salesRepCommissionRuleId}");

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // Validate no commission was created
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, 
                "No Sales Rep Commission should be created when case date is after contract end date.");

            TestContext.WriteLine($"✓ Validated no commission created when case date is after contract end date.");
        }

        [TestMethod]
        public async Task StandaloneCommissionWithPhysicianHappyPath()
        {
            /**
            * Scenario: Standalone commission is created with physician matched
            * 
            * Variation:
            * - supplierId = supplier
            * 
            * Expected outcome:
            * - Standalone commission is created with the supplier matched
            */

            // Step 1: Case setup
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
            * Rep Role is set to SR (SalesRep) to indicate this is a standalone sales rep commission.
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
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standalone commission
                }
            );
            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with SR role: {salesRepContractId}");
            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
            * - Commission Rule Type = Standalone
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match can be set to control whether physician must match
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standalone commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var overrideRate = 67m; 
            var commissionSplit = 100m;  // Use 100% of commission
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    HospitalId = hospital,
                    PhysicianId = physician,
                    RequiredSalesRepMatch = false,  // No specific sales rep match required in happy path
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standalone commission)
                    OverrideRate = overrideRate,  // Use override rate for testing
                }
            );
            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");

           /* Step 2: Validate standalone commission creation
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAsync(caseRecord);
            Assert.IsTrue(
                salesRepCommissions.Any(c => c.GetProperty("leap_physicianid").GetString() == physician.ToString()),
                "Standalone commission should be created with the physician matched."
            );

            TestContext.WriteLine("Standalone commission created successfully with physician matched.");  */
        }

        [TestMethod]
        public async Task StandaloneCommissionWithSupplierHappyPath()
        {
            /**
            * Scenario: Standalone commission is created with supplier matched
            * 
            * Variation:
            * - supplierId = supplier
            * 
            * Expected outcome:
            * - Standalone commission is created with the supplier matched
            */

            // Step 1: Case setup
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
                    supplier, // Ensure SupplierId is passed correctly
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
            * Rep Role is set to SR (SalesRep) to indicate this is a standalone sales rep commission.
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
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standalone commission
                }
            );
            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with SR role: {salesRepContractId}");
            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
            * - Commission Rule Type = Standalone
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match can be set to control whether physician must match
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standalone commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var overrideRate = 67m; 
            var commissionSplit = 100m;  // Use 100% of commission
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    HospitalId = hospital,
                    PhysicianId = physician,
                    OverrideRate = overrideRate,  // Use override rate for testing
                    SupplierId = supplier,  // Match on supplier for standalone commission
                    RequiredSalesRepMatch = false,  // No specific sales rep match required in happy path
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standalone commission)
                }
            );
            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");
         
          
        }

        [TestMethod]
        public async Task StandaloneCommissionWithManufactureMatched()
        {
            /**
            * Scenario: Standalone commission is created with manufacturer matched
            * 
            * Variation:
            * - manufacturerId = manufacturer
            * 
            * Expected outcome:
            * - Standalone commission is created with the manufacturer matched
            */

            // Step 1: Case setup
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
                    supplier, // Ensure SupplierId is passed correctly
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
            * Rep Role is set to SR (SalesRep) to indicate this is a standalone sales rep commission.
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
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standalone commission
                }
            );
            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with SR role: {salesRepContractId}");
            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
            * - Commission Rule Type = Standalone
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match can be set to control whether physician must match
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standalone commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var overrideRate = 67m;
            var commissionSplit = 100m;  // Use 100% of commission
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    HospitalId = hospital,
                    PhysicianId = physician,
                    ManufacturerId = manufacturer,  // Match on manufacturer for standalone commission
                    RequiredSalesRepMatch = false,  // No specific sales rep match required in happy path
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standalone commission)
                    OverrideRate = overrideRate,  // Use override rate for testing
                }
            );
            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");

         
        }

        [TestMethod]
        public async Task StandaloneCommissionWithMultifieldsMatched()
        {
            /**
            * Scenario: Standalone commission is created with multifields matched
            * 
            * Variation:
            * - manufacturerId = manufacturer
            * - supplierId = supplier
            * 
            * Expected outcome:
            * - Standalone commission is created with the multifields matched
            */

            // Step 1: Case setup
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
                    supplier, // Ensure SupplierId is passed correctly
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
            * Rep Role is set to SR (SalesRep) to indicate this is a standalone sales rep commission.
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
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standalone commission
                }
            );
            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with SR role: {salesRepContractId}");
            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
            * - Commission Rule Type = Standalone
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match can be set to control whether physician must match
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standalone commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var overrideRate = 67m; 
            var commissionSplit = 100m;  // Use 100% of commission
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    HospitalId = hospital,
                    PhysicianId = physician,
                    SupplierId = supplier,  // Match on supplier for standalone commission
                    ManufacturerId = manufacturer,  // Match on manufacturer for standalone commission
                    ProductFamilyId = productFamily, // Match on product family for standalone commission
                    RequiredSalesRepMatch = false,  // No specific sales rep match required in happy path
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standalone commission)
                    OverrideRate = overrideRate,  // Use override rate for testing
                }
            );
            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");


          
        }

        [TestMethod]
        public async Task StandaloneCommissionRequireSalesRepMatchTrueAndMatches()
        {
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
            var physician = Seed.PhysicianId;  // Use OwnerUserId as physician to ensure sales rep matches
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
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standard commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with SR role: {salesRepContractId}");

            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
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
            var overrideRate = 67m; 
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    OverrideRate = overrideRate,  // Use override rate for testing
                    RequiredSalesRepMatch = true,  // Variation: Require sales rep match
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect Standalone commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");

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
            Assert.AreEqual(
                salesRepCommissions.Count,
                1,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            // Get the Standalone commission
            var commission = salesRepCommissions.First(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32()
                        == OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone
                    && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standalone Sales Rep Commission should be created.");

            // Validate commission relationships
           /*  ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
            ); */

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
        public async Task StandaloneCommissionRequireSalesRepMatchTrueAndDoesNotMatch()
        {
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
            * Rep Role is set to SR (SalesRep) to indicate this is a standalone sales rep commission.
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
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standalone commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with SR role: {salesRepContractId}");

            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
            * - Commission Rule Type = Standalone
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = true (variation)
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standard commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission
            var overrideRate = 67m; 
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = true,  // Variation: Require sales rep match
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standalone commission)
                    OverrideRate = overrideRate,  // Use override rate for testing
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");

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
            Assert.AreEqual(
                salesRepCommissions.Count,
                0,
                "No Sales Rep Commission should be created when sales rep does not match and RequireSalesRepMatch is true."
            );

            TestContext.WriteLine($"No commission created as expected for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionRequireSalesRepMatchNull()
        {
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
            * Rep Role is set to SR (SalesRep) to indicate this is a standalone sales rep commission.
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
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standalone commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with SR role: {salesRepContractId}");

            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = null (variation)
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standalone commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission
            var overrideRate = 35m; 
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    OverrideRate = overrideRate,  // Use override rate for testing
                    RequiredSalesRepMatch = null,  // Variation: Require Sales Rep Match is null
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standalone commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");

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
            Assert.AreEqual(
                salesRepCommissions.Count,
                1,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            // Get the Standalone commission
            var commission = salesRepCommissions.First(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32()
                        == OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone
                    && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standalone Sales Rep Commission should be created.");

            // Validate commission relationships
          /*   ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
            ); */

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
        public async Task StandaloneCommissionRequireSalesRepMatchFalseRegardlessOfMatch()
        {
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
            * Rep Role is set to SR (SalesRep) to indicate this is a standalone sales rep commission.
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
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standalone commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with SR role: {salesRepContractId}");

            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
            * - Commission Rule Type = Standalone
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = false (variation)
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect Standalone commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission
            var overrideRate = 67m; 
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    OverrideRate = overrideRate,  // Use override rate for testing
                    RequiredSalesRepMatch = false,  // Variation: Do not require sales rep match
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standalone commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");

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
            Assert.AreEqual(
                 1, salesRepCommissions.Count,               
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            // Get the Standard commission
            var commission = salesRepCommissions.First(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32()
                        == OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone
                    && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standalone Sales Rep Commission should be created.");

            // Validate commission relationships
           /* ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
            ); */

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
        public async Task StandardCommissionMoreThanOneCommission()
        {
            /**
            * Scenario: Commission is created when Require Sales Rep Match is null
            * 
            * Based on: Happy Path
            * Variation:
            * - more than one case product added
            * 
            * Expected outcome:
            * - more than one Commission is created
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

            var productCostPrice2 = 150.00m;
            var product2 = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice2
            );

            Assert.IsNotNull(product2, "Product 2 record should exist after setup.");

              // Create Case Product2 with Stock & Bill product type
            var cpUnitPrice2 = 300.00m;
            var cpQuantity2 = 2;
            var caseProduct2 = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    manufacturer,
                    manufacturer,
                    productFamily,
                    product2
                ) with
                {
                    Quantity = cpQuantity2,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = true,
                    UnitPrice = cpUnitPrice2,
                }
            );

              Assert.IsNotNull(caseProduct2, "Case Product 2 record should exist after setup.");
            TestContext.WriteLine($"Created Product 2 and Case Product 2 for Case ID: {caseRecord}");

            /**
            * step 3: create sales rep contract with SR rep role
            * 
            * The contract specifies commission rate ranges (High/Low) for SalesRep role.
            * Rep Role is set to SR (SalesRep) to indicate this is a standalone sales rep commission.
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
                    Name = $"Standalone Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,  // SR role for standalone commission
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with SR role: {salesRepContractId}");

            /**
            * step 4: create Standalone commission rule
            *
            * This rule defines when the standalone commission is paid:
            * - Commission Rule Type = Standard
            * - Commission Split = percentage of commission to allocate
            * - Start/End dates determine when rule is active
            * - Require Sales Rep Match = null (variation)
            * - Case date must fall within this date range for commission to apply
            * - Block VP Standard flag should not affect standalone commission creation
            */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;  // Use 100% of commission
            var overrideRate = 67m; 
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues
                        .SalesRepCommissionRule
                        .CommissionType
                        .Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesRepId = Config.OwnerUserId,
                    OverrideRate = overrideRate,  // Use override rate for testing
                    RequiredSalesRepMatch = null,  // Variation: Require Sales Rep Match is null
                    BlockVPstandardCommission = false,  // Block VP Standard can be false (does not affect standalone commission)
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standalone): {salesRepCommissionRuleId}");

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
            Assert.AreEqual(
                salesRepCommissions.Count,
                2,
                "Only 2 Sales Rep Commission should be created for this Case and Rule."
            );

            // Get the Standalone commission
            var commission = salesRepCommissions.First(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32()
                        == OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone
                    && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standalone Sales Rep Commission should be created.");

            // Validate commission relationships
           /*  ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
            ); */

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

    }
}
