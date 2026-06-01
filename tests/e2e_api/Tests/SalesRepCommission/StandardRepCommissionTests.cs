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
            TestContext.WriteLine("-----Starting Setup/Creation-----");

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

            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );
            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
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
             */
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;
            var repMaxRate = 7m;
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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            /**
             * step 4: create Standard commission rule
             */
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            /**
             * step 5: trigger case regeneration and await completion
             */
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
             * step 6: validate sales rep commission creation and field values
             */
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions?.Count ?? 0}");
            Assert.IsNotNull(salesRepCommissions, "Commission query should not return null.");
            Assert.AreEqual(
                1,
                salesRepCommissions.Count,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            var commission = salesRepCommissions.Single(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.Standard
                       && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standard Sales Rep Commission should be created.");

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

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: repMaxRate,
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

            commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
            Assert.AreEqual(
                commissionSplit,
                commissionSplitValue.GetDecimal(),
                "Commission Split should match the rule configuration."
            );

            // Validate anticipated commission was calculated and applied to usage form detail
            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(usageFormDetails.Count > 0, "There should be at least 1 Usage Form Detail for the Case.");

            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var anticipatedCommissionProperty);
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(
                    anticipatedCommission > 0,
                    "Anticipated Sales Rep Commission should be calculated on Usage Form Detail."
                );

                TestContext.WriteLine($"Anticipated Commission on Usage Form Detail: {anticipatedCommission}");
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionRequireSalesRepMatchTrueAndMatches()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;
            var repMaxRate = 7m;
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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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
                    RequiredSalesRepMatch = true,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions?.Count ?? 0}");
            Assert.IsNotNull(salesRepCommissions);
            Assert.AreEqual(
                1,
                salesRepCommissions.Count,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            var commission = salesRepCommissions.Single(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.Standard
                       && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standard Sales Rep Commission should be created.");

            ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
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

            var resolvedUnitPrice = ResolveUnitPrice(cpUnitPrice, null, null);

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: repMaxRate,
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

            commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
            Assert.AreEqual(
                commissionSplit,
                commissionSplitValue.GetDecimal(),
                "Commission Split should match the rule configuration."
            );

            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(usageFormDetails.Count > 0, "There should be at least 1 Usage Form Detail for the Case.");

            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var anticipatedCommissionProperty);
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(anticipatedCommission > 0, "Anticipated Sales Rep Commission should be calculated on Usage Form Detail.");
                TestContext.WriteLine($"Anticipated Commission on Usage Form Detail: {anticipatedCommission}");
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionRequireSalesRepMatchTrueAndDoesNotMatch()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;
            var repMaxRate = 7m;
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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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
                    RequiredSalesRepMatch = true,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions?.Count ?? 0}");
            Assert.IsNotNull(salesRepCommissions);
            Assert.AreEqual(
                0,
                salesRepCommissions.Count,
                "No Sales Rep Commission should be created when sales rep does not match and RequireSalesRepMatch is true."
            );

            TestContext.WriteLine($"No commission created as expected for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionRequireSalesRepMatchFalseRegardlessOfMatch()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;
            var repMaxRate = 7m;
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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions?.Count ?? 0}");
            Assert.IsNotNull(salesRepCommissions);
            Assert.AreEqual(
                1,
                salesRepCommissions.Count,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            var commission = salesRepCommissions.Single(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.Standard
                       && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standard Sales Rep Commission should be created.");

            ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
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

            var resolvedUnitPrice = ResolveUnitPrice(cpUnitPrice, null, null);

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: repMaxRate,
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

            commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
            Assert.AreEqual(
                commissionSplit,
                commissionSplitValue.GetDecimal(),
                "Commission Split should match the rule configuration."
            );

            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(usageFormDetails.Count > 0, "There should be at least 1 Usage Form Detail for the Case.");

            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var anticipatedCommissionProperty);
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(anticipatedCommission > 0, "Anticipated Sales Rep Commission should be calculated on Usage Form Detail.");
                TestContext.WriteLine($"Anticipated Commission on Usage Form Detail: {anticipatedCommission}");
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionRequireSalesRepMatchNull()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;
            var repMaxRate = 7m;
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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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
                    RequiredSalesRepMatch = null,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions?.Count ?? 0}");
            Assert.IsNotNull(salesRepCommissions);
            Assert.AreEqual(
                1,
                salesRepCommissions.Count,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            var commission = salesRepCommissions.Single(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.Standard
                       && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standard Sales Rep Commission should be created.");

            ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
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

            var resolvedUnitPrice = ResolveUnitPrice(cpUnitPrice, null, null);

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: repMaxRate,
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

            commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
            Assert.AreEqual(
                commissionSplit,
                commissionSplitValue.GetDecimal(),
                "Commission Split should match the rule configuration."
            );

            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(usageFormDetails.Count > 0, "There should be at least 1 Usage Form Detail for the Case.");

            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var anticipatedCommissionProperty);
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(anticipatedCommission > 0, "Anticipated Sales Rep Commission should be calculated on Usage Form Detail.");
                TestContext.WriteLine($"Anticipated Commission on Usage Form Detail: {anticipatedCommission}");
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionNotCreatedWhenStandaloneCommissionsExist()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;
            var repMaxRate = 7m;

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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(standaloneSalesRepContractId, "Standalone Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Standalone Sales Rep Contract with SR role: {standaloneSalesRepContractId}");

            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var standaloneSalesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone,
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
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(standaloneSalesRepCommissionRuleId, "Standalone Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Standalone Sales Rep Commission Rule: {standaloneSalesRepCommissionRuleId}");

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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(standardSalesRepContractId, "Standard Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Standard Sales Rep Contract with SR role: {standardSalesRepContractId}");

            var standardSalesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(standardSalesRepCommissionRuleId, "Standard Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Standard Sales Rep Commission Rule: {standardSalesRepCommissionRuleId}");

            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, standaloneSalesRepCommissionRuleId);

            Assert.IsNotNull(standaloneCommissions);
            Assert.AreEqual(1, standaloneCommissions.Count, "Standalone commission should be created.");

            var standaloneCommission = standaloneCommissions.Single(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone
                       && ruleId.GetGuid() == standaloneSalesRepCommissionRuleId;
            });

            Assert.IsNotNull(standaloneCommission, "Standalone Sales Rep Commission should be created.");
            TestContext.WriteLine($"Standalone commission created as expected for Case ID: {caseRecord}");

            var standardCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, standardSalesRepCommissionRuleId);

            TestContext.WriteLine($"Commissions returned: {standardCommissions?.Count ?? 0}");
            Assert.IsNotNull(standardCommissions);
            Assert.AreEqual(
                0,
                standardCommissions.Count,
                "No Standard Sales Rep Commission should be created when Standalone commissions exist."
            );

            TestContext.WriteLine($"No Standard commission created as expected for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionCreatedWhenNoStandaloneCommissionsExist()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;
            var repMaxRate = 7m;

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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions?.Count ?? 0}");
            Assert.IsNotNull(salesRepCommissions);
            Assert.AreEqual(
                1,
                salesRepCommissions.Count,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            var commission = salesRepCommissions.Single(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.Standard
                       && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standard Sales Rep Commission should be created.");

            ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
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

            var resolvedUnitPrice = ResolveUnitPrice(cpUnitPrice, null, null);

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: repMaxRate,
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

            commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
            Assert.AreEqual(
                commissionSplit,
                commissionSplitValue.GetDecimal(),
                "Commission Split should match the rule configuration."
            );

            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(usageFormDetails.Count > 0, "There should be at least 1 Usage Form Detail for the Case.");

            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var anticipatedCommissionProperty);
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(anticipatedCommission > 0, "Anticipated Sales Rep Commission should be calculated on Usage Form Detail.");
                TestContext.WriteLine($"Anticipated Commission on Usage Form Detail: {anticipatedCommission}");
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionFullContractRateAppliedForNonSRRoles()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;
            var repMaxRate = 7m;
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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.VP,
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract with VP role: {salesRepContractId}");

            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions?.Count ?? 0}");
            Assert.IsNotNull(salesRepCommissions);
            Assert.AreEqual(
                1,
                salesRepCommissions.Count,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            var commission = salesRepCommissions.Single(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.Standard
                       && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standard Sales Rep Commission should be created.");

            ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
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

            var resolvedUnitPrice = ResolveUnitPrice(cpUnitPrice, null, null);

            // NOTE: Preserving your original expectation here
            var fullContractRate = 100m;
            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: fullContractRate,
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

            commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
            Assert.AreEqual(
                commissionSplit,
                commissionSplitValue.GetDecimal(),
                "Commission Split should match the rule configuration."
            );

            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(usageFormDetails.Count > 0, "There should be at least 1 Usage Form Detail for the Case.");

            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var anticipatedCommissionProperty);
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(anticipatedCommission > 0, "Anticipated Sales Rep Commission should be calculated on Usage Form Detail.");
                TestContext.WriteLine($"Anticipated Commission on Usage Form Detail: {anticipatedCommission}");
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task StandardCommissionSRRateReducedWhenASRCSRCommissionsExist()
        {
            TestContext.WriteLine($"Starting test: {TestContext.TestName}");

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Orthopedics);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 150.00m;
            var product = await _products.SetupProductAsync(
                organizationId, subjectId, unitGroupId, unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var cpUnitPrice = 300.00m;
            var cpQuantity = 2;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = cpQuantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");
            TestContext.WriteLine($"Created Product and Case Product for Case ID: {caseRecord}");

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var srMinRate = 3m;
            var srMaxRate = 7m;
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

            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var srSalesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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

            var asrSalesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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

            var csrSalesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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

            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            var srCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, srSalesRepCommissionRuleId);
            Assert.IsNotNull(srCommissions);
            Assert.AreEqual(1, srCommissions.Count, "Expected exactly 1 SR commission for SR rule.");

            var srCommission = srCommissions.Single(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.Standard
                       && ruleId.GetGuid() == srSalesRepCommissionRuleId;
            });

            var asrCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, asrSalesRepCommissionRuleId);
            Assert.IsNotNull(asrCommissions);
            Assert.AreEqual(1, asrCommissions.Count, "Expected exactly 1 ASR commission for ASR rule.");
            var asrCommission = asrCommissions.Single();

            var csrCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, csrSalesRepCommissionRuleId);
            Assert.IsNotNull(csrCommissions);
            Assert.AreEqual(1, csrCommissions.Count, "Expected exactly 1 CSR commission for CSR rule.");
            var csrCommission = csrCommissions.Single();

            var expectedSrRate = srMaxRate - (asrMaxRate + csrMaxRate);

            var resolvedUnitPrice = ResolveUnitPrice(cpUnitPrice, null, null);

            ValidateCommissionRateAndAmount(
                commission: srCommission,
                expectedRate: expectedSrRate,
                unitPrice: resolvedUnitPrice,
                quantity: cpQuantity,
                testContext: TestContext
            );

            ValidateCommissionRateAndAmount(
                commission: asrCommission,
                expectedRate: asrMaxRate,
                unitPrice: resolvedUnitPrice,
                quantity: cpQuantity,
                testContext: TestContext
            );

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
            * Expected outcome:
            * - High commission rate is used (repMaxRate)
            */

            // step 1: case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // step 2: products and case products setup
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

            // step 3: create supplier contract and commission rule with >20% commission
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

            // step 4: create sales rep contract with SR rep role
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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            // step 5: create Standard commission rule
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            // step 6: trigger case regeneration and await completion
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // step 7: validate EXACTLY ONE commission and validate it uses HIGH rate
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions?.Count ?? 0}");
            Assert.IsNotNull(salesRepCommissions, "Commission query should not return null.");
            Assert.AreEqual(
                1,
                salesRepCommissions.Count,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            var commission = salesRepCommissions.Single(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.Standard
                    && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standard Sales Rep Commission should be created.");

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

            // Validate commission rate and amount (should use HIGH rate)
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
            Assert.IsTrue(usageFormDetails.Count > 0, "There should be at least 1 Usage Form Detail for the Case.");

            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var anticipatedCommissionProperty);
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(anticipatedCommission > 0, "Anticipated Sales Rep Commission should be calculated on Usage Form Detail.");
                TestContext.WriteLine($"Anticipated Commission on Usage Form Detail: {anticipatedCommission}");
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
            * Expected outcome:
            * - Low commission rate is used (repMinRate)
            */

            // step 1: case setup
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");
            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // step 2: products and case products setup
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

            // step 3: create supplier contract and commission rule with <=20% commission
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

            // step 4: create sales rep contract with SR rep role
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var repMinRate = 3m;
            var repMaxRate = 7m;
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
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            Assert.IsNotNull(salesRepContractId, "Sales Rep Contract should be created.");
            TestContext.WriteLine($"Created Sales Rep Contract: {salesRepContractId}");

            // step 5: create Standard commission rule
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;
            var commissionSplit = 100m;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standard,
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
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            Assert.IsNotNull(salesRepCommissionRuleId, "Sales Rep Commission Rule should be created.");
            TestContext.WriteLine($"Created Sales Rep Commission Rule (Standard): {salesRepCommissionRuleId}");

            // step 6: trigger case regeneration and await completion
            await _cases.RegenCaseAsync(caseRecord);
            TestContext.WriteLine($"Case regeneration started for Case ID: {caseRecord}");

            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));
            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            // step 7: validate EXACTLY ONE commission and validate it uses LOW rate
            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);

            TestContext.WriteLine($"Commissions returned: {salesRepCommissions?.Count ?? 0}");
            Assert.IsNotNull(salesRepCommissions, "Commission query should not return null.");
            Assert.AreEqual(
                1,
                salesRepCommissions.Count,
                "Only 1 Sales Rep Commission should be created for this Case and Rule."
            );

            var commission = salesRepCommissions.Single(c =>
            {
                c.TryGetProperty("leap_commissiontype", out var ruleType);
                c.TryGetProperty("_leap_salesrepcommissionruleid_value", out var ruleId);
                return ruleType.GetInt32() == OptionSetValues.SalesRepCommissionRule.CommissionType.Standard
                    && ruleId.GetGuid() == salesRepCommissionRuleId;
            });

            Assert.IsNotNull(commission, "Standard Sales Rep Commission should be created.");

            ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: salesRepCommissionRuleId,
                testContext: TestContext
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

            var resolvedUnitPrice = ResolveUnitPrice(cpUnitPrice, null, null);

            // Validate commission rate and amount (should use LOW rate)
            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: repMinRate,
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

            commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
            Assert.AreEqual(
                commissionSplit,
                commissionSplitValue.GetDecimal(),
                "Commission Split should match the rule configuration."
            );

            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(usageFormDetails.Count > 0, "There should be at least 1 Usage Form Detail for the Case.");

            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var anticipatedCommissionProperty);
                var anticipatedCommission = anticipatedCommissionProperty.GetDecimal();

                Assert.IsTrue(anticipatedCommission > 0, "Anticipated Sales Rep Commission should be calculated on Usage Form Detail.");
                TestContext.WriteLine($"Anticipated Commission on Usage Form Detail: {anticipatedCommission}");
            }

            TestContext.WriteLine($"All assertions completed successfully for Case ID: {caseRecord}");
        }

    }
}