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
    [TestCategory("Override")]
    public class OverrideRepCommissionTest : BaseSalesRepCommissionTest
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
        private UsageFormDetailService _usageFormDetails = null!;
        private SalesRepCommissionService _salesRepCommissions = null!;
        private SystemUserService _systemUsers = null!;

        [TestInitialize]
        public void Setup()
        {
            _accounts = new AccountFixture(Client, Config);
            _cases = new CaseFixture(Client, Config);
            _products = new ProductFixture(Client, Config);
            _caseProducts = new CaseProductFixture(Client, Config);
            _salesRepContracts = new SalesRepContractFixture(Client, Config);
            _salesRepCommissionRules = new SalesRepCommissionRuleFixture(Client, Config);

            _usageFormDetails = new UsageFormDetailService(Client);
            _salesRepCommissions = new SalesRepCommissionService(Client);
            _systemUsers = new SystemUserService(Client);
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

        // ============ TEST 3.1: OverrideCommissionHappyPath ============
        [TestMethod]
        public async Task OverrideCommissionHappyPath()
        {
            /**
            * 3.1 OverrideCommissionHappyPath
            * 
            * Test validates that Override commission is created with all required fields set correctly.
            * - OverrideRate (required) = 7.5m
            * - CommissionSplit (required) = 100m
            * - Effective dates bracket the case date
            * - Matching fields ensure rule applies: Supplier, Manufacturer, ProductFamily, Product
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var productCostPrice = 100.00m;
            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: productCostPrice
            );

            var cpUnitPrice = 200.00m;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = cpUnitPrice,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    Name = $"Override Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var overrideRate = 7.5m;
            var commissionSplit = 100m;
            var ruleStartDate = contractStartDate;
            var ruleEndDate = contractEndDate;

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Override Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    OverrideRate = overrideRate,
                    SupplierId = supplier,
                    ManufacturerId = manufacturer,
                    ProductFamilyId = productFamily,
                    ProductId = product,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Only 1 Override Commission should be created.");

            var commission = salesRepCommissions.First();

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
                expectedRate: overrideRate,
                unitPrice: cpUnitPrice,
                quantity: 1,
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
                testContext: TestContext
            );

            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.IsTrue(usageFormDetails.Count > 0, "Usage Form Details should exist.");
            foreach (var ufd in usageFormDetails)
            {
                ufd.TryGetProperty("leap_anticipatedsalesrepcommission", out var commissionProperty);
                var anticipatedCommission = commissionProperty.GetDecimal();
                Assert.IsTrue(anticipatedCommission > 0, "Anticipated commission should be calculated.");
            }

            TestContext.WriteLine($"✓ Override Commission Happy Path validated successfully.");
        }

        // ============ TEST 3.2: OverrideCommission_DoesNotGenerate_WhenStandaloneCommissionExists ============
        [TestMethod]
        public async Task OverrideCommission_DoesNotGenerate_WhenStandaloneCommissionExists()
        {
            /**
            * 3.2 OverrideCommission_DoesNotGenerate_WhenStandaloneCommissionExists
            *
            * Goal: When a Standalone commission is created for the case, Override commission should not be created.
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

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
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var standaloneSalesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                }
            );

            var standaloneRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: standaloneSalesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                }
            );

            var overrideSalesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var overrideRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: overrideSalesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 5m,
                    ManufacturerId = manufacturer,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "Standalone commission should be created.");

            var overrideCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, overrideRuleId);
            Assert.AreEqual(0, overrideCommissions.Count, "No Override commission should be created when Standalone commission already exists.");

            TestContext.WriteLine($"✓ Override commission was correctly suppressed when a standalone commission exists.");
        }

        // ============ TEST 3.3: OverrideCommission_RequiredSalesRepMatchNull_ShouldGenerateRegardless ============
        [TestMethod]
        public async Task OverrideCommission_RequiredSalesRepMatchNull_ShouldGenerateRegardless()
        {
            /**
            * 3.3 OverrideCommission_RequiredSalesRepMatchNull_ShouldGenerateRegardless
            *
            * Goal: When RequiredSalesRepMatch is null, Override commission should generate even when the configured sales rep does not match.
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

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
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var otherUserId = Seed.OrganizationId;
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = otherUserId,
                }
            );

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 5m,
                    ManufacturerId = manufacturer,
                    SalesRepId = otherUserId,
                    // RequiredSalesRepMatch omitted intentionally to test null behavior
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Override commission should be created when RequiredSalesRepMatch is null.");

            TestContext.WriteLine($"✓ Override commission generated successfully when RequiredSalesRepMatch is null.");
        }

        // ============ TEST 3.4: OverrideCommission_UsesDefaultUnitPrice ============
        [TestMethod]
        public async Task OverrideCommission_UsesDefaultUnitPrice()
        {
            /**
            * 3.4 OverrideCommission_UsesDefaultUnitPrice
            * 
            * Goal: Verify default unit price is used when no overrides are set.
            * - UnitPrice = 200m
            * - OverrideUnitPrice = not set
            * - IOUnitPrice = not set
            * - ResolveUnitPrice(200, null, null) should return 200m
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamily, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var defaultUnitPrice = 200.00m;
            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = defaultUnitPrice,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var overrideRate = 6.0m;
            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = overrideRate,
                    ManufacturerId = manufacturer,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Override commission should be created.");

            var commission = salesRepCommissions.First();
            var resolvedUnitPrice = ResolveUnitPrice(defaultUnitPrice, null, null);
            Assert.AreEqual(defaultUnitPrice, resolvedUnitPrice, "Resolved unit price should be default.");

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: overrideRate,
                unitPrice: resolvedUnitPrice,
                quantity: 1,
                testContext: TestContext
            );

            TestContext.WriteLine($"✓ Commission correctly calculated using default unit price {defaultUnitPrice}m.");
        }

        // ============ TEST 3.5: OverrideCommission_ProductFamilyMatch_ShouldGenerate ============
        [TestMethod]
        public async Task OverrideCommission_ProductFamilyMatch_ShouldGenerate()
        {
            /**
            * 3.5 OverrideCommission_ProductFamilyMatch_ShouldGenerate
            * 
            * Goal: Verify override commission generates when ProductFamilyId matches.
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

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
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 5m,
                    SupplierId = supplier,
                    ManufacturerId = manufacturer,
                    ProductFamilyId = productFamily,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Override commission should generate for product family match.");

            TestContext.WriteLine($"✓ Override commission correctly generated for product family match.");
        }

        // ============ TEST 3.6: OverrideCommission_ProductFamilyMismatch_ShouldNotGenerate ============
        [TestMethod]
        public async Task OverrideCommission_ProductFamilyMismatch_ShouldNotGenerate()
        {
            /**
            * 3.6 OverrideCommission_ProductFamilyMismatch_ShouldNotGenerate
            * 
            * Goal: Verify override commission does NOT generate when ProductFamilyId doesn't match.
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamilyA = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var productFamilyB = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamilyB, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamilyB, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 5m,
                    SupplierId = supplier,
                    ManufacturerId = manufacturer,
                    ProductFamilyId = productFamilyA,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, "No commission should be created for product family mismatch.");

            TestContext.WriteLine($"✓ Correctly validated no commission for product family mismatch.");
        }

        // ============ TEST 3.7: OverrideCommission_ProductFamilyEmpty_ShouldUseFallbackRule ============
        [TestMethod]
        public async Task OverrideCommission_ProductFamilyEmpty_ShouldUseFallbackRule()
        {
            /**
            * 3.7 OverrideCommission_ProductFamilyEmpty_ShouldUseFallbackRule
            * 
            * Goal: When multiple rules exist, fallback rule with empty ProductFamilyId should be used.
            * - RuleSpecific: ProductFamilyId = PF_A (mismatch)
            * - RuleFallback: ProductFamilyId = null (fallback, should match)
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamilyA = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var productFamilyB = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId,
                manufacturer, productFamilyB, OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamilyB, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var ruleSpecificId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 5m,
                    SupplierId = supplier,
                    ManufacturerId = manufacturer,
                    ProductFamilyId = productFamilyA,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var ruleFallbackId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 4m,
                    SupplierId = supplier,
                    ManufacturerId = manufacturer,
                    ProductFamilyId = null,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var ruleSpecificCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, ruleSpecificId);
            Assert.AreEqual(0, ruleSpecificCommissions.Count, "Specific rule should not match.");

            var ruleFallbackCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, ruleFallbackId);
            Assert.AreEqual(1, ruleFallbackCommissions.Count, "Fallback rule should match.");

            TestContext.WriteLine($"✓ Fallback rule correctly used when specific rule does not match.");
        }

        // ============ TEST 3.8: OverrideCommission_MultipleMatchingRules_ShouldUseMostSpecificRule ============
        [TestMethod]
        public async Task OverrideCommission_MultipleMatchingRules_ShouldUseMostSpecificRule()
        {
            /**
            * 3.8 OverrideCommission_MultipleMatchingRules_ShouldUseMostSpecificRule
            * 
            * Goal: When multiple matching rules exist, engine should select the most specific one.
            * Specificity hierarchy: Physician > Hospital > Product > Product Family
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

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
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var ruleProductFamily = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 3.0m,
                    SupplierId = supplier,
                    ManufacturerId = manufacturer,
                    ProductFamilyId = productFamily,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var ruleProduct = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 4.0m,
                    SupplierId = supplier,
                    ManufacturerId = manufacturer,
                    ProductId = product,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var ruleHospital = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 5.0m,
                    SupplierId = supplier,
                    ManufacturerId = manufacturer,
                    HospitalId = hospital,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var rulePhysician = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 6.0m,
                    SupplierId = supplier,
                    ManufacturerId = manufacturer,
                    PhysicianId = physician,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var physicianCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, rulePhysician);
            Assert.AreEqual(1, physicianCommissions.Count, "Most specific rule (Physician) should generate commission.");

            var hospitalCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, ruleHospital);
            var productCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, ruleProduct);
            var productFamilyCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, ruleProductFamily);

            TestContext.WriteLine($"✓ Most specific rule (Physician) correctly selected for override commission.");
        }

        // ============ TEST 3.9: OverrideCommission_CaseDateBeforeStartDate_ShouldNotGenerate ============
        [TestMethod]
        public async Task OverrideCommission_CaseDateBeforeStartDate_ShouldNotGenerate()
        {
            /**
            * 3.9 OverrideCommission_CaseDateBeforeStartDate_ShouldNotGenerate
            * 
            * Goal: Commission should NOT generate when case date is before rule start date.
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow.Date;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

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
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var ruleStartDate = caseDateTime.AddDays(1);
            var ruleEndDate = ruleStartDate.AddYears(1);

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = ruleEndDate,
                    OverrideRate = 5m,
                    ManufacturerId = manufacturer,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, "No commission when case date is before rule start date.");

            TestContext.WriteLine($"✓ Correctly prevented commission for case date before rule start date.");
        }

        // ============ TEST 3.10: OverrideCommission_CaseDateAfterEndDate_ShouldNotGenerate ============
        [TestMethod]
        public async Task OverrideCommission_CaseDateAfterEndDate_ShouldNotGenerate()
        {
            /**
            * 3.10 OverrideCommission_CaseDateAfterEndDate_ShouldNotGenerate
            * 
            * Goal: Commission should NOT generate when case date is after rule end date.
            */

            var ruleStartDate = DateTime.UtcNow.Date.AddDays(-30);
            var ruleEndDate = DateTime.UtcNow.Date;
            var caseDateTime = ruleEndDate.AddDays(1);

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

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
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date.AddDays(-60);
            var contractEndDate = DateTime.UtcNow.Date.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: ruleStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = ruleEndDate,
                    OverrideRate = 5m,
                    ManufacturerId = manufacturer,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, "No commission when case date is after rule end date.");

            TestContext.WriteLine($"✓ Correctly prevented commission for case date after rule end date.");
        }

        // ============ TEST 3.11: OverrideCommission_RequireRepMatch_RepDoesNotMatch_ShouldNotGenerate ============
        [TestMethod]
        public async Task OverrideCommission_RequireRepMatch_RepDoesNotMatch_ShouldNotGenerate()
        {
            /**
            * 3.11 OverrideCommission_RequireRepMatch_RepDoesNotMatch_ShouldNotGenerate
            * 
            * Goal: When RequiredSalesRepMatch = true, commission should NOT generate if rep doesn't match.
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

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
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var otherUserId = Seed.OrganizationId;
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = otherUserId,
                }
            );

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 5m,
                    ManufacturerId = manufacturer,
                    RequiredSalesRepMatch = true,
                    SalesRepId = otherUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, "No commission when RequiredSalesRepMatch=true and rep does not match.");

            TestContext.WriteLine($"✓ Correctly prevented commission when RequiredSalesRepMatch=true and rep mismatch.");
        }

        // ============ TEST 3.12: OverrideCommission_DistributorMismatch_ShouldNotGenerate ============
        [TestMethod]
        public async Task OverrideCommission_DistributorMismatch_ShouldNotGenerate()
        {
            /**
            * 3.12 OverrideCommission_DistributorMismatch_ShouldNotGenerate
            * 
            * Goal: Commission should NOT generate when distributor does not match rule's distributor.
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

            var distributorA = await _accounts.SetupDistributorAsync();
            var distributorB = await _accounts.SetupDistributorAsync();

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
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 5m,
                    ManufacturerId = manufacturer,
                    DistributorId = distributorA,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, "No commission when distributor does not match rule's distributor.");

            TestContext.WriteLine($"✓ Correctly prevented commission for distributor mismatch.");
        }

        // ============ TEST 3.13: OverrideCommission_RequireRepMatch_RepMatches_ShouldGenerate ============
        [TestMethod]
        public async Task OverrideCommission_RequireRepMatch_RepMatches_ShouldGenerate()
        {
            /**
            * 3.13 OverrideCommission_RequireRepMatch_RepMatches_ShouldGenerate
            * 
            * Goal: When RequiredSalesRepMatch = true and rep matches, commission should generate.
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

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
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 5m,
                    ManufacturerId = manufacturer,
                    RequiredSalesRepMatch = true,
                    SalesRepId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Commission should generate when RequiredSalesRepMatch=true and rep matches.");

            TestContext.WriteLine($"✓ Commission correctly generated when RequiredSalesRepMatch=true and rep matches.");
        }

        // ============ TEST 3.14: OverrideCommission_OwningBusinessUnitMatch_ShouldGenerate ============
        [TestMethod]
        public async Task OverrideCommission_OwningBusinessUnitMatch_ShouldGenerate()
        {
            /**
            * 3.14 OverrideCommission_OwningBusinessUnitMatch_ShouldGenerate
            * 
            * Goal: When contract is Distributor type and OwningBusinessUnitId matches the owner's business unit, commission should generate.
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var patientId = $"PAT-{Guid.NewGuid()}";
            var caseDateTime = DateTime.UtcNow;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure, patientId, caseDateTime);

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

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
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            // Get the business unit of the owner
            var systemUser = await _systemUsers.GetAsync(Config.OwnerUserId);
            systemUser.TryGetProperty("_businessunitid_value", out var ownerBusinessUnitId);
            Assert.IsNotNull(ownerBusinessUnitId, "Owner user should have a business unit.");

            // Use Distributor contract type - which matches on OwningBusinessUnitId
            var distributorContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.Distributor,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EndDate = contractEndDate,
                }
            );

            var salesRepCommissionRuleId = await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Override,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: Config.OwnerUserId,
                    salesRepContractId: distributorContractId
                ) with
                {
                    EndDate = contractEndDate,
                    OverrideRate = 5m,
                    ManufacturerId = manufacturer,
                    OwningBusinessUnitId = ownerBusinessUnitId.GetGuid(),
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseRecord, salesRepCommissionRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Commission should generate when OwningBusinessUnitId matches for Distributor contract.");

            ValidateCommissionOwnership(
                commission: salesRepCommissions.First(),
                expectedOwnerId: Config.OwnerUserId,
                expectedOwningBusinessUnitId: ownerBusinessUnitId.GetGuid(),
                testContext: TestContext
            );

            TestContext.WriteLine($"✓ Commission correctly generated when OwningBusinessUnitId matches for Distributor contract.");
        }
    }
}
