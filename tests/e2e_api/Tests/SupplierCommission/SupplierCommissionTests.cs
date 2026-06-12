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
    public class SupplierCommissionTests : BaseTest
    {
        public TestContext TestContext { get; set; } = null!;

        // Fixtures
        private AccountFixture _accounts = null!;
        private CaseFixture _cases = null!;
        private ContactFixture _contacts = null!;
        private ProductFixture _products = null!;
        private CaseProductFixture _caseProducts = null!;
        private SupplierContractFixture _supplierContracts = null!;
        private SupplierCommissionRuleFixture _supplierCommissionRules = null!;
        private PricingTierFixture _pricingTiers = null!;

        // Services
        private UsageFormService _usageForms = null!;
        private SupplierCommissionService _supplierCommissions = null!;
        private UsageFormDetailService _usageFormDetails = null!;
        private PricingTierService _pricingTierService = null!;

        [TestInitialize]
        public void Setup()
        {
            _accounts = new AccountFixture(Client, Config);
            _cases = new CaseFixture(Client, Config);
            _products = new ProductFixture(Client, Config);
            _caseProducts = new CaseProductFixture(Client, Config);
            _supplierContracts = new SupplierContractFixture(Client, Config);
            _supplierCommissionRules = new SupplierCommissionRuleFixture(Client, Config);
            _pricingTiers = new PricingTierFixture(Client, Config);
            _contacts = new ContactFixture(Client, Config);

            _usageForms = new UsageFormService(Client);
            _supplierCommissions = new SupplierCommissionService(Client);
            _usageFormDetails = new UsageFormDetailService(Client);
            _pricingTierService = new PricingTierService(Client);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (_caseProducts != null)
                await _caseProducts.CleanupAsync();
            if (_cases != null)
                await _cases.CleanupAsync();
            if (_supplierCommissionRules != null)
                await _supplierCommissionRules.CleanupAsync();
            if (_pricingTiers != null)
                await _pricingTiers.CleanupAsync();
            if (_supplierContracts != null)
                await _supplierContracts.CleanupAsync();
            if (_products != null)
                await _products.CleanupAsync();
            if (_accounts != null)
                await _accounts.CleanupAsync();
        }

        [TestMethod]
        public async Task HospitalOwnedStockAndBill()
        {
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
            // supplier setup
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var supplier = await _accounts.SetupSupplierAsync();

            // stock and bill product
            var sbProductFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.StockAndBill
            );
            var sbProduct = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                sbProductFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: 100.00m
            );

            Assert.IsNotNull(sbProduct, "Stock&Bill Product record should exist after setup.");

            var sbCaseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    supplier,
                    sbProductFamily,
                    sbProduct
                ) with
                {
                    Quantity = 2,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = true,
                    UnitPrice = 150.00m,
                }
            );

            Assert.IsNotNull(
                sbCaseProduct,
                "Stock&Bill Case Product record should exist after setup."
            );

            /**
            * step 3: create supplier contract and commission rule
            */
            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
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
                    name: $"Test Rule {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 10.0m,
                    HospitalOwnedOverride = true,
                }
            );

            TestContext.WriteLine($"Supplier Contract and Rules created for Case ID: {caseRecord}");

            /**
            * step 4: trigger case regeneration and await completion
            */
            // trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(2)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 5: validate no supplier commission on usage form details
            */
            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);

            Assert.AreEqual(
                2,
                usageFormDetails.Count,
                "There should be exactly 2 Usage Form Details for the Case."
            );

            foreach (var ufd in usageFormDetails)
            {
                Assert.IsFalse(
                    ufd.TryGetProperty("leap_anticipatedsuppliercommission", out _),
                    "Supplier Commission should be null for hospital owned Stock&Bill products."
                );
            }

            TestContext.WriteLine($"Assertions completed for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task HospitalOwnedConsignmentFlagFalse()
        {
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
            // supplier setup
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var supplier = await _accounts.SetupSupplierAsync();

            // consignment product
            var conProductFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );
            var conProduct = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                conProductFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            Assert.IsNotNull(conProduct, "Consignment Product record should exist after setup.");

            var conCaseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    supplier,
                    conProductFamily,
                    conProduct
                ) with
                {
                    Quantity = 2,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = true,
                    UnitPrice = 150.00m,
                }
            );

            Assert.IsNotNull(
                conCaseProduct,
                "Consignment Case Product record should exist after setup."
            );

            /**
            * step 3: create supplier contract and commission rule
            */
            // contract setup
            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
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

            // rule setup
            var supplierCommissionRuleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 10.0m,
                    HospitalOwnedOverride = false,
                }
            );

            TestContext.WriteLine($"Supplier Contract and Rules created for Case ID: {caseRecord}");

            /**
            * step 4: trigger case regeneration and await completion
            */
            // trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(2)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 5: validate no supplier commission on usage form details
            */
            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);

            Assert.AreEqual(
                2,
                usageFormDetails.Count,
                "There should be exactly 2 Usage Form Details for the Case."
            );

            foreach (var ufd in usageFormDetails)
            {
                Assert.IsFalse(
                    ufd.TryGetProperty("leap_anticipatedsuppliercommission", out _),
                    "Supplier Commission should be null for Consignment with HospitalOwnedOverride = false."
                );
            }

            TestContext.WriteLine($"Assertions completed for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task HospitalOwnedConsignmentFlagNull()
        {
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
            // supplier setup
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var supplier = await _accounts.SetupSupplierAsync();

            // consignment product
            var conProductFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );
            var conProduct = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                conProductFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            Assert.IsNotNull(conProduct, "Consignment Product record should exist after setup.");

            var conCaseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    supplier,
                    conProductFamily,
                    conProduct
                ) with
                {
                    Quantity = 2,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = true,
                    UnitPrice = 150.00m,
                }
            );

            Assert.IsNotNull(
                conCaseProduct,
                "Consignment Case Product record should exist after setup."
            );

            /**
            * step 3: create supplier contract and commission rule
            */
            // contract setup
            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
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

            // rule setup
            var supplierCommissionRuleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 10.0m,
                }
            );

            // explicitly set HospitalOwnedOverride to null since default will be false
            await _supplierCommissionRules.UpdateAsync(
                supplierCommissionRuleId,
                new SupplierCommissionRuleUpdateConfig
                {
                    HospitalOwnedOverride = Optional<bool>.Null,
                }
            );

            TestContext.WriteLine($"Supplier Contract and Rules created for Case ID: {caseRecord}");

            /**
            * step 4: trigger case regeneration and await completion
            */
            // trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(2)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 5: validate no supplier commission on usage form details
            */
            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);

            Assert.AreEqual(
                2,
                usageFormDetails.Count,
                "There should be exactly 2 Usage Form Details for the Case."
            );

            foreach (var ufd in usageFormDetails)
            {
                Assert.IsFalse(
                    ufd.TryGetProperty("leap_anticipatedsuppliercommission", out _),
                    "Supplier Commission should be null for Consignment with HospitalOwnedOverride = null."
                );
            }

            TestContext.WriteLine($"Assertions completed for Case ID: {caseRecord}");
        }

        [TestMethod]
        public async Task HospitalOwnedConsignmentFlagTrue()
        {
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
            // supplier setup
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var supplier = await _accounts.SetupSupplierAsync();

            // consignment product
            var conProductFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );
            var conProduct = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                conProductFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            Assert.IsNotNull(conProduct, "Consignment Product record should exist after setup.");

            var conCaseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    supplier,
                    conProductFamily,
                    conProduct
                ) with
                {
                    Quantity = 2,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = true,
                    UnitPrice = 150.00m,
                }
            );

            Assert.IsNotNull(
                conCaseProduct,
                "Consignment Case Product record should exist after setup."
            );

            /**
            * step 3: create supplier contract and commission rule
            */
            // contract setup
            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
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

            // rule setup
            var supplierCommissionRuleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 10.0m,
                    HospitalOwnedOverride = true,
                }
            );

            TestContext.WriteLine($"Supplier Contract and Rules created for Case ID: {caseRecord}");

            /**
            * step 4: trigger case regeneration and await completion
            */
            // trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(2)
            );

            TestContext.WriteLine($"Case regeneration completed for Case ID: {caseRecord}");

            /**
            * step 5: validate no supplier commission on usage form details
            */
            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);

            Assert.AreEqual(
                2,
                usageFormDetails.Count,
                "There should be exactly 2 Usage Form Details for the Case."
            );

            foreach (var ufd in usageFormDetails)
            {
                decimal supplierCommissionTotal = ufd.GetProperty(
                        "leap_anticipatedsuppliercommission"
                    )
                    .GetDecimal();

                Assert.AreEqual(
                    15.00m,
                    supplierCommissionTotal,
                    "Supplier Commission Total on Usage Form Detail should be 0 for hospital owned Consignment products."
                );
            }

            TestContext.WriteLine($"Assertions completed for Case ID: {caseRecord}");
        }

        // ============ TEST 1006: SupplierCommission_MultipleMatchingRules_ShouldUseMostSpecificRule ============
        [TestMethod]
        public async Task SupplierCommission_MultipleMatchingRulesHospitalandPhysician_ShouldUseMostSpecificRule()
        {
            /**
            * 3.8 SupplierCommission_MultipleMatchingRules_ShouldUseMostSpecificRule
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
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                 organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier, OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
               organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily, OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var SupplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = true,
                    AllPhysicians = true,
                }
            );

            var ruleHospital = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Hospital {Config.CorrelationId}",
                    supplierContractId: SupplierContractId,
                    statusCode: Config.DefaultStatusCode,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    HospitalId = hospital,
                    CommissionPercent = 7.0m,
                    EffectiveStartDate = contractStartDate,
                }
            );

            var rulePhysician = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Physician {Config.CorrelationId}",
                    statusCode: Config.DefaultStatusCode,
                    ownerId: Config.OwnerUserId,
                    supplierContractId: SupplierContractId
                ) with
                {
                    PhysicianId = physician,
                    CommissionPercent = 10.0m,
                    EffectiveStartDate = contractStartDate,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var physicianCommissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, rulePhysician);
            Assert.AreEqual(1, physicianCommissions.Count, "Most specific rule (Physician) should generate commission.");

            var hospitalCommissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, ruleHospital);
            Assert.AreEqual(0, hospitalCommissions.Count, "Most specific rule (Hospital) should generate commission.");

            TestContext.WriteLine($"✓ Most specific rule (Physician) correctly selected for supplier commission.");
        }

        [TestMethod]
        public async Task SupplierCommission_MultipleMatchingRulesProductandProductFamily_ShouldUseMostSpecificRule()
        {
            /**
            * 3.8 SupplierCommission_MultipleMatchingRules_ShouldUseMostSpecificRule
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
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                 organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier, OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
               organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily, OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var SupplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = true,
                    AllPhysicians = true,
                }
            );


            var ruleProductFamily = await _supplierCommissionRules.SetupAsync(
               SupplierCommissionRuleConfig.Default(
                   name: $"Test Rule Product Family {Config.CorrelationId}",
                   statusCode: Config.DefaultStatusCode,
                   ownerId: Config.OwnerUserId,
                   supplierContractId: SupplierContractId
               ) with
               {
                   EffectiveStartDate = contractStartDate,
                   ProductFamilyId = productFamily,                   
                   CommissionPercent = 10.0m,
               }
           );

            var ruleProduct = await _supplierCommissionRules.SetupAsync(
               SupplierCommissionRuleConfig.Default(
                   name: $"Test Rule Product {Config.CorrelationId}",
                   supplierContractId: SupplierContractId,
                   statusCode: Config.DefaultStatusCode,
                   ownerId: Config.OwnerUserId
               ) with
               {
                   EffectiveStartDate = contractStartDate,
                   CommissionPercent = 4.0m,
                   ProductId = product,
               }
           );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var productCommissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, ruleProduct);
            Assert.AreEqual(1, productCommissions.Count, "Most specific rule (Product) should generate commission.");

            var productFamilyCommissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, ruleProductFamily);
            Assert.AreEqual(0, productFamilyCommissions.Count, "Most specific rule (ProductFamily) should generate commission.");

            TestContext.WriteLine($"✓ Most specific rule (Product and ProductFamily) correctly selected for supplier commission.");
        }

        [TestMethod]
        public async Task SupplierCommission_MultipleMatchingRulesHospitalandProduct_ShouldUseMostSpecificRule()
        {
            /**
            * 3.8 SupplierCommission_MultipleMatchingRules_ShouldUseMostSpecificRule
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
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;

            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;
            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                 organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier, OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
               organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily, OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var SupplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = false,
                    AllPhysicians = true,
                }
            );

            var ruleProduct = await _supplierCommissionRules.SetupAsync(
               SupplierCommissionRuleConfig.Default(
                   name: $"Test Rule Product {Config.CorrelationId}",
                   supplierContractId: SupplierContractId,
                   statusCode: Config.DefaultStatusCode,
                   ownerId: Config.OwnerUserId
               ) with
               {
                   EffectiveStartDate = contractStartDate,
                   CommissionPercent = 4.0m,
                   ProductId = product,
               }
           );

            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Hospital Match {Config.CorrelationId}",
                    supplierContractId: SupplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveStartDate = contractStartDate,
                    HospitalId = hospital,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with HospitalId: {hospital} (matching case hospital)"
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            // Verify commission was generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                1,
                commissions.Count,
                "When rule has non-empty hospital field matching case hospital and contract has AllHospitals=false, should generate supplier commission."
            );

            var productCommissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, ruleProduct);
            Assert.AreEqual(0, productCommissions.Count, "Most specific rule (Product) should not generate commission.");

            TestContext.WriteLine($"✓ Most specific rule (Hospital and Product) correctly selected for supplier commission.");
        }

        [TestMethod]
        public async Task SupplierCommission_PricingTierMatch_ShouldNotGenerateCommission()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Woundcare);
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 150.00m,
                }
            );

            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
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

            var pricingTierId = await _pricingTiers.SetupAsync(
                PricingTierConfig.Default(
                    supplierContractId: supplierContractId,
                    productId: product,
                    ownerId: Config.OwnerUserId,
                    name: $"Test Pricing Tier {Config.CorrelationId}"
                ) with
                {
                    PriceLowerBound = 100.00m,
                    PriceUpperBound = 200.00m,
                    CommissionRate = 10.0m,
                }
            );

            Assert.IsNotNull(pricingTierId, "Pricing tier record should exist after setup.");
    
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Match {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 10.0m,
                    ProductId = product,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, ruleId);
            Assert.AreEqual(0, commissions.Count, "A pricing tier match should suppress supplier commission generation.");
        }

        [TestMethod]
        public async Task SupplierCommission_PricingTierMismatch_ShouldGenerateCommission()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(OptionSetValues.Account.SalesCategory.Woundcare);
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 250.00m,
                }
            );

            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
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

            var pricingTierId = await _pricingTiers.SetupAsync(
                PricingTierConfig.Default(
                    supplierContractId: supplierContractId,
                    productId: product,
                    ownerId: Config.OwnerUserId,
                    name: $"Test Pricing Tier {Config.CorrelationId}"
                ) with
                {
                    PriceLowerBound = 100.00m,
                    PriceUpperBound = 200.00m,
                    CommissionRate = 10.0m,
                }
            );

            Assert.IsNotNull(pricingTierId, "Pricing tier record should exist after setup.");

            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Mismatch {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    CommissionPercent = 10.0m,
                    ProductId = product,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, ruleId);
            Assert.AreEqual(1, commissions.Count, "A pricing tier mismatch should generate a supplier commission record.");
        }

        [TestMethod]
        public async Task SupplierCommission_HospitalFieldPopulated_WithMatchingHospital_ShouldGenerateCommission()
        {
            /**
            * Goal: When a supplier commission rule has a populated (non-empty) hospital field 
            * that matches the case's hospital, and the supplier contract has AllHospitals=false 
            * and AllPhysicians=false, the engine should generate a supplier commission record.
            * 
            * Key difference from other tests:
            * - AllHospitals = false (not true)     
            * - Rule HospitalId is explicitly set (not empty)
            * - Matching is based on specific hospital field, not global "all hospitals" flag
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            TestContext.WriteLine($"Created Case with ID: {caseRecord} for Hospital: {hospital}");

            // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

            // Create supplier contract with AllHospitals=false, AllPhysicians=false
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = false,
                    AllPhysicians = true,
                }
            );

            Assert.IsNotNull(
                supplierContractId,
                "Supplier Contract record should exist after setup."
            );

            TestContext.WriteLine(
                $"Supplier Contract created with AllHospitals=false, AllPhysicians=false"
            );

            // Create supplier commission rule with populated HospitalId matching the case's hospital
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Hospital Match {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveStartDate = contractStartDate,
                    HospitalId = hospital,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with HospitalId: {hospital} (matching case hospital)"
            );

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                1,
                commissions.Count,
                "When rule has non-empty hospital field matching case hospital and contract has AllHospitals=false, should generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly generated for matching hospital rule."
            );
        }

        [TestMethod]
        public async Task SupplierCommission_PhysicianFieldPopulated_WithMatchingPhysician_ShouldGenerateCommission()
        {
            /**
                * Key difference from other tests:
            * - AllPhysicians = false (not true)     
            * - Rule PhysicianId is explicitly set (not empty)
            * - Matching is based on specific physician field, not global "all physicians" flag
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            TestContext.WriteLine($"Created Case with ID: {caseRecord} for Hospital: {hospital}");

            // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

            // Create supplier contract with AllHospitals=false, AllPhysicians=false
            var contractStartDate = DateTime.UtcNow.Date;

            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = true,
                    AllPhysicians = false,
                }
            );

            Assert.IsNotNull(
                supplierContractId,
                "Supplier Contract record should exist after setup."
            );

            TestContext.WriteLine(
                $"Supplier Contract created with AllHospitals=false, AllPhysicians=false"
            );

            // Create supplier commission rule with populated HospitalId matching the case's hospital
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Physician Match {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveStartDate = contractStartDate,
                    PhysicianId = physician,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with PhysicianId: {physician} (matching case physician)"
            );

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                1,
                commissions.Count,
                "When rule has non-empty physician field matching case physician and contract has AllPhysicians=false, should generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly generated for matching physician rule."
            );
        }

        [TestMethod]
        public async Task SupplierCommission_PhysicianFieldPopulated_WithNonMatchingPhysician_ShouldNotGenerateCommission()
        {
            /**
            * Goal: When a supplier commission rule has a populated (non-empty) physician field 
            * that does NOT match the physician of the case, and the supplier 
            * contract has AllHospitals=true and AllPhysicians=false, the engine should NOT generate 
            * a supplier commission record.
            * 
            * Key test scenario:
            * - Case is created for Physician1
            * - Rule is configured with Physician2 (different from case's physician)
            * - Rule has non-matching PhysicianId (not empty, but doesn't match case)
            * - AllHospitals = true and AllPhysicians = false
            * - Expected result: NO commission generated due to physician mismatch
            */

            var hospital = Seed.HospitalId;
            var physician1 = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician1, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            TestContext.WriteLine($"Created Case with ID: {caseRecord} for Physician 1: {physician1}");

            // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );

            Assert.IsNotNull(productFamily, "Product Family record should exist after setup.");

            TestContext.WriteLine($"Created Product Family with ID: {productFamily}");

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            // Create case product using Physician1
            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            // Create supplier contract with AllHospitals=false, AllPhysicians=false
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = true,
                    AllPhysicians = false,
                }
            );


            // Create a DIFFERENT physician for the rule (Physician2)
            var physician2 = await _contacts.SetupPhysicianAsync();
            Assert.IsNotNull(physician2, "Physician 2 record should exist after setup.");
            TestContext.WriteLine($"Created Physician 2 with ID: {physician2} (different from case physician)");

            // Create supplier commission rule with Physician2 (which does NOT match the case's Physician1)
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Physician Non-Match {Config.CorrelationId}",
                    statusCode: Config.DefaultStatusCode,
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveEndDate = contractEndDate,
                    PhysicianId = physician2,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with PhysicianId: {physician2} (NON-MATCHING - case uses {physician1})"
            );

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was NOT generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                0,
                commissions.Count,
                "When rule has non-empty physician field that does NOT match case physician and contract has AllPhysicians=false, should NOT generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly NOT generated for non-matching physician rule."
            );
        }

        [TestMethod]
        public async Task SupplierCommission_ProductFamilyFieldPopulated_WithMatchingProductFamily_ShouldGenerateCommission()
        {
            /**
            * Goal: When a supplier commission rule has a populated (non-empty) product family field 
            * that matches the product family of the product in the case product, and the supplier 
            * contract has AllHospitals=false and AllPhysicians=false, the engine should generate 
            * a supplier commission record.
            * 
            * Key difference from hospital match test:
            * - Matching is based on ProductFamilyId field, not HospitalId
            * - ProductFamilyId is explicitly set in the rule (not empty)
            * - ProductFamilyId must match the product family of the product used in the case product
            * - AllHospitals = false and AllPhysicians = false
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.Consignment
            );

            Assert.IsNotNull(productFamily, "Product Family record should exist after setup.");

            TestContext.WriteLine($"Created Product Family with ID: {productFamily}");

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            TestContext.WriteLine($"Created Product with ID: {product} in Product Family: {productFamily}");


            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, manufacturer, productFamily, product) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

            TestContext.WriteLine(
                $"Created Case Product with Product Family: {productFamily}"
            );

            // Create supplier contract with AllHospitals=false, AllPhysicians=false
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = false,
                    AllPhysicians = false,
                }
            );

            Assert.IsNotNull(
                supplierContractId,
                "Supplier Contract record should exist after setup."
            );


            // Create supplier commission rule with populated ProductFamilyId matching the case product's family
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Product Family Match {Config.CorrelationId}",
                    statusCode: Config.DefaultStatusCode,
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    HospitalId = hospital,
                    PhysicianId = physician,
                    EffectiveEndDate = contractEndDate,
                    ProductFamilyId = productFamily,
                    ProductId = product,
                    OwnerId = Config.OwnerUserId,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with ProductFamilyId: {productFamily} (matching case product family)"
            );

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                1,
                commissions.Count,
                "When rule has non-empty product family field matching case product family and contract has AllHospitals=false, should generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly generated for matching product family rule."
            );
        }

        [TestMethod]
        public async Task SupplierCommission_ProductFieldPopulated_WithMatchingProduct_ShouldGenerateCommission()
        {
            /**
            * Goal: When a supplier commission rule has a populated (non-empty) product field 
            * that matches the product used in the case product, and the supplier 
            * contract has AllHospitals=false and AllPhysicians=false, the engine should generate 
            * a supplier commission record.
            * 
            * Key difference from product family match test:
            * - Matching is based on ProductId field (exact product), not ProductFamilyId
            * - ProductId is explicitly set in the rule (not empty)
            * - ProductId must match the exact product used in the case product
            * - AllHospitals = false and AllPhysicians = false
            * - More specific matching than product family
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );
            var supplier = await _accounts.SetupSupplierAsync();

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.Consignment
            );

            Assert.IsNotNull(productFamily, "Product Family record should exist after setup.");

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            TestContext.WriteLine($"Created Product with ID: {product}");

            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

            TestContext.WriteLine(
                $"Created Case Product with Product: {product}"
            );

            // Create supplier contract with AllHospitals=false, AllPhysicians=false
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = false,
                    AllPhysicians = false,
                }
            );

            Assert.IsNotNull(
                supplierContractId,
                "Supplier Contract record should exist after setup."
            );

            TestContext.WriteLine(
                $"Supplier Contract created with AllHospitals=false, AllPhysicians=false"
            );

            // Create supplier commission rule with populated ProductId matching the case product's product
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Product Match {Config.CorrelationId}",
                    statusCode: Config.DefaultStatusCode,
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    HospitalId = hospital,
                    PhysicianId = physician,
                    EffectiveEndDate = contractEndDate,
                    ProductId = product,
                    OwnerId = Config.OwnerUserId,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with ProductId: {product} (matching case product)"
            );

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                1,
                commissions.Count,
                "When rule has non-empty product field matching case product and contract has AllHospitals=false, should generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly generated for matching product rule."
            );
        }

        [TestMethod]
        public async Task SupplierCommission_ProductFamilyFieldPopulated_WithNonMatchingProductFamily_ShouldNotGenerateCommission()
        {
            /**
            * Goal: When a supplier commission rule has a populated (non-empty) product family field 
            * that does NOT match the product family of the product in the case product, and the supplier 
            * contract has AllHospitals=false and AllPhysicians=false, the engine should NOT generate 
            * a supplier commission record.
            * 
            * Key test scenario:
            * - Two different product families are created
            * - Case product uses ProductFamily1
            * - Rule is configured with ProductFamily2 (different from case product)
            * - Rule has non-matching ProductFamilyId (not empty, but doesn't match case)
            * - AllHospitals = false and AllPhysicians = false
            * - Expected result: NO commission generated due to product family mismatch
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );
            var supplier = await _accounts.SetupSupplierAsync();

            // Create FIRST product family - this will be used in the case product
            var productFamily1 = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.Consignment
            );

            Assert.IsNotNull(productFamily1, "Product Family 1 record should exist after setup.");

            TestContext.WriteLine($"Created Product Family 1 with ID: {productFamily1}");

            // Create SECOND product family - this will be used in the rule (non-matching)
            var productFamily2 = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.Consignment
            );

            Assert.IsNotNull(productFamily2, "Product Family 2 record should exist after setup.");

            TestContext.WriteLine($"Created Product Family 2 with ID: {productFamily2} (different from case product family)");

            // Create product in ProductFamily1
            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily1,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            TestContext.WriteLine($"Created Product with ID: {product} in Product Family 1: {productFamily1}");

            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    manufacturer,
                    productFamily1,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

            TestContext.WriteLine(
                $"Created Case Product with Product Family 1: {productFamily1}"
            );

            // Create supplier contract with AllHospitals=false, AllPhysicians=false
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = false,
                    AllPhysicians = false,
                }
            );

            Assert.IsNotNull(
                supplierContractId,
                "Supplier Contract record should exist after setup."
            );

            TestContext.WriteLine(
                $"Supplier Contract created with AllHospitals=false, AllPhysicians=false"
            );

            // Create supplier commission rule with ProductFamilyId2 (which does NOT match the case product's ProductFamily1)
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Product Family Non-Match {Config.CorrelationId}",
                    statusCode: Config.DefaultStatusCode,
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    HospitalId = hospital,
                    PhysicianId = physician,
                    EffectiveEndDate = contractEndDate,
                    ProductFamilyId = productFamily2,
                    OwnerId = Config.OwnerUserId,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with ProductFamilyId: {productFamily2} (NON-MATCHING - case uses {productFamily1})"
            );

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was NOT generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                0,
                commissions.Count,
                "When rule has non-empty product family field that does NOT match case product family and contract has AllHospitals=false, should NOT generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly NOT generated for non-matching product family rule."
            );
        }

        [TestMethod]
        public async Task SupplierCommission_ProductFieldPopulated_WithNonMatchingProduct_ShouldNotGenerateCommission()
        {
            /**
            * Goal: When a supplier commission rule has a populated (non-empty) product field 
            * that does NOT match the product used in the case product, and the supplier 
            * contract has AllHospitals=false and AllPhysicians=false, the engine should NOT generate 
            * a supplier commission record.
            * 
            * Key test scenario:
            * - Two different products are created (both in the same product family)
            * - Case product uses Product1
            * - Rule is configured with Product2 (different from case product)
            * - Rule has non-matching ProductId (not empty, but doesn't match case)
            * - AllHospitals = false and AllPhysicians = false
            * - Expected result: NO commission generated due to product mismatch
            */

            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            TestContext.WriteLine($"Created Case with ID: {caseRecord}");

            // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );
            var supplier = await _accounts.SetupSupplierAsync();

            // Create ONE product family (both products will be in this family)
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                OptionSetValues.Product.ProductType.Consignment
            );

            Assert.IsNotNull(productFamily, "Product Family record should exist after setup.");

            TestContext.WriteLine($"Created Product Family with ID: {productFamily}");

            // Create FIRST product - this will be used in the case product
            var product1 = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            Assert.IsNotNull(product1, "Product 1 record should exist after setup.");

            TestContext.WriteLine($"Created Product 1 with ID: {product1}");

            // Create SECOND product - this will be used in the rule (non-matching)
            var product2 = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 150.00m
            );

            Assert.IsNotNull(product2, "Product 2 record should exist after setup.");

            TestContext.WriteLine($"Created Product 2 with ID: {product2} (different from case product)");

            // Create case product using Product1
            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    manufacturer,
                    productFamily,
                    product1
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

            TestContext.WriteLine(
                $"Created Case Product with Product 1: {product1}"
            );

            // Create supplier contract with AllHospitals=false, AllPhysicians=false
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = false,
                    AllPhysicians = false,
                }
            );

            Assert.IsNotNull(
                supplierContractId,
                "Supplier Contract record should exist after setup."
            );

            TestContext.WriteLine(
                $"Supplier Contract created with AllHospitals=false, AllPhysicians=false"
            );

            // Create supplier commission rule with Product2 (which does NOT match the case product's Product1)
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Product Non-Match {Config.CorrelationId}",
                    statusCode: Config.DefaultStatusCode,
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    HospitalId = hospital,
                    PhysicianId = physician,
                    EffectiveEndDate = contractEndDate,
                    ProductId = product2,
                    OwnerId = Config.OwnerUserId,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with ProductId: {product2} (NON-MATCHING - case uses {product1})"
            );

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was NOT generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                0,
                commissions.Count,
                "When rule has non-empty product field that does NOT match case product and contract has AllHospitals=false, should NOT generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly NOT generated for non-matching product rule."
            );
        }

        [TestMethod]
        public async Task SupplierCommission_HospitalFieldPopulated_WithNonMatchingHospital_ShouldNotGenerateCommission()
        {
            /**
            * Goal: When a supplier commission rule has a populated (non-empty) hospital field 
            * that does NOT match the hospital of the case, and the supplier 
            * contract has AllHospitals=false and AllPhysicians=true, the engine should NOT generate 
            * a supplier commission record.
            * 
            * Key test scenario:
            * - Case is created for Hospital1
            * - Rule is configured with Hospital2 (different from case's hospital)
            * - Rule has non-matching HospitalId (not empty, but doesn't match case)
            * - AllHospitals = false and AllPhysicians = true
            * - Expected result: NO commission generated due to hospital mismatch
            */

            var hospital1 = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital1, physician, procedure);

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            TestContext.WriteLine($"Created Case with ID: {caseRecord} for Hospital 1: {hospital1}");

            // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );
            var supplier = await _accounts.SetupSupplierAsync();

            // Create product family
            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );

            Assert.IsNotNull(productFamily, "Product Family record should exist after setup.");

            TestContext.WriteLine($"Created Product Family with ID: {productFamily}");

            // Create product
            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            TestContext.WriteLine($"Created Product with ID: {product}");

            // Create case product using Hospital1
            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital1,
                    supplier,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

            TestContext.WriteLine(
                $"Created Case Product with Hospital 1: {hospital1}"
            );

            // Create supplier contract with AllHospitals=false, AllPhysicians=false
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);

            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: contractStartDate,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = false,
                    AllPhysicians = true,
                }
            );

            Assert.IsNotNull(
                supplierContractId,
                "Supplier Contract record should exist after setup."
            );

            TestContext.WriteLine(
                $"Supplier Contract created with AllHospitals=false, AllPhysicians=false"
            );

            // Create a DIFFERENT hospital for the rule (Hospital2)
            var hospital2 = await _accounts.SetupHospitalAsync();
            Assert.IsNotNull(hospital2, "Hospital 2 record should exist after setup.");
            TestContext.WriteLine($"Created Hospital 2 with ID: {hospital2} (different from case hospital)");

            // Create supplier commission rule with Hospital2 (which does NOT match the case's Hospital1)
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Hospital Non-Match {Config.CorrelationId}",
                    statusCode: Config.DefaultStatusCode,
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveEndDate = contractEndDate,
                    HospitalId = hospital2,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with HospitalId: {hospital2} (NON-MATCHING - case uses {hospital1})"
            );

            // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was NOT generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                0,
                commissions.Count,
                "When rule has non-empty hospital field that does NOT match case hospital and contract has AllHospitals=false, should NOT generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly NOT generated for non-matching hospital rule."
            );
        }

        [TestMethod]
        public async Task SupplierContract_ContractWithPhysicianContactinListMatch_ShouldGenerateCommission()
        {
            /**
            * Goal: Create a supplier contract, then create a contact record called "physician", 
            * and add the contact to the leap_leap_suppliercontract_contact entity relationship.
            * 
            * Steps:
            * 1. Setup supplier account
            * 2. Create supplier contract
            * 3. Create contact record (physician)
            * 4. Add contact to supplier contract relationship
            * 5. Verify the relationship was created
            */

            // Step 1: Setup supplier
            var supplier = await _accounts.SetupSupplierAsync();
            Assert.IsNotNull(supplier, "Supplier account should be created.");
            TestContext.WriteLine($"Created Supplier with ID: {supplier}");

            // Step 2: Create supplier contract
            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: DateTime.UtcNow.Date,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = true,
                    AllPhysicians = false,
                }
            );
            Assert.IsNotNull(supplierContractId, "Supplier Contract should be created.");
            TestContext.WriteLine($"Created Supplier Contract with ID: {supplierContractId}");

            // Step 3: Create contact record (physician)
            var physicianContact = await _contacts.SetupPhysicianAsync();          
            Assert.IsNotNull(physicianContact, "Physician Contact should be created.");
            TestContext.WriteLine($"Created Physician Contact with ID: {physicianContact}");

            // Step 4: Add contact to supplier contract relationship
            await Client.AddReferenceAsync(
                sourceEntityLogicalName: EntityNames.SupplierContract,
                sourceId: supplierContractId,
                navigationPropertyName: "leap_leap_suppliercontract_contact",  /* "leap_suppliercontract_contact", */
                targetEntityLogicalName: EntityNames.Contact,
                targetId: physicianContact
            );
            TestContext.WriteLine(
                $"Added Contact {physicianContact} to Supplier Contract {supplierContractId} relationship."
            );

            // Step 5: Verify the relationship was created by retrieving the supplier contract
            var supplierContractRecord = await Client.GetAsync(
                EntityNames.SupplierContract,
                supplierContractId
            );
            Assert.IsNotNull(supplierContractRecord, "Supplier Contract record should exist.");
            TestContext.WriteLine(
                "✓ Supplier Contract with Contact relationship successfully created and verified."
            );

            var hospital = Seed.HospitalId;                        
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physicianContact, procedure);

              // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );            

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

             // Create supplier commission rule with populated HospitalId matching the case's hospital
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Physician Match {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveStartDate = DateTime.UtcNow.Date,
                    PhysicianId = physicianContact,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with PhysicianId: {physicianContact} (matching case physician)"
            );

             // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                1,
                commissions.Count,
                "When rule has non-empty physician field matching case physician and contract has AllPhysicians=false, should generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly generated for matching physician rule."
            );

        }

        [TestMethod]
        public async Task SupplierContract_ContractWithPhysicianContactinListNoMatch_ShouldNotGenerateCommission()
        {
            /**
            * Goal: Create a supplier contract, then create a contact record called "physician", 
            * and add the contact to the leap_leap_suppliercontract_contact entity relationship.
            * 
            * Steps:
            * 1. Setup supplier account
            * 2. Create supplier contract
            * 3. Create contact record (physician)
            * 4. Add contact to supplier contract relationship
            * 5. Verify the relationship was created
            */

            // Step 1: Setup supplier
            var supplier = await _accounts.SetupSupplierAsync();
            Assert.IsNotNull(supplier, "Supplier account should be created.");
            TestContext.WriteLine($"Created Supplier with ID: {supplier}");

            // Step 2: Create supplier contract
            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: DateTime.UtcNow.Date,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = true,
                    AllPhysicians = false,
                }
            );
            Assert.IsNotNull(supplierContractId, "Supplier Contract should be created.");
            TestContext.WriteLine($"Created Supplier Contract with ID: {supplierContractId}");

            // Step 3: Create contact record (physician)
            var physicianContact = await _contacts.SetupPhysicianAsync();          
            Assert.IsNotNull(physicianContact, "Physician Contact should be created.");
            TestContext.WriteLine($"Created Physician Contact with ID: {physicianContact}");

            // Step 4: Add contact to supplier contract relationship
            await Client.AddReferenceAsync(
                sourceEntityLogicalName: EntityNames.SupplierContract,
                sourceId: supplierContractId,
                navigationPropertyName: "leap_leap_suppliercontract_contact",  /* "leap_suppliercontract_contact", */
                targetEntityLogicalName: EntityNames.Contact,
                targetId: physicianContact
            );
            TestContext.WriteLine(
                $"Added Contact {physicianContact} to Supplier Contract {supplierContractId} relationship."
            );

            // Step 5: Verify the relationship was created by retrieving the supplier contract
            var supplierContractRecord = await Client.GetAsync(
                EntityNames.SupplierContract,
                supplierContractId
            );
            Assert.IsNotNull(supplierContractRecord, "Supplier Contract record should exist.");
            TestContext.WriteLine(
                "✓ Supplier Contract with Contact relationship successfully created and verified."
            );

            var hospital = Seed.HospitalId;    
            var physician1 = Seed.PhysicianId;                    
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician1, procedure);

              // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );            

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

             // Create supplier commission rule with populated HospitalId matching the case's hospital
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Physician Match {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveStartDate = DateTime.UtcNow.Date,
                    PhysicianId = physicianContact,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with PhysicianId: {physicianContact} (matching case physician)"
            );

             // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                1,
                commissions.Count,
                "When rule has non-empty physician field matching case physician and contract has AllPhysicians=false, should generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly generated for matching physician rule."
            );

        }

        [TestMethod]
        public async Task SupplierContract_ContractWithHospitalinListMatch_ShouldGenerateCommission()
        {
            /**
            * Goal: Create a supplier contract, then create a contact record called "physician", 
            * and add the contact to the leap_leap_suppliercontract_contact entity relationship.
            * 
            * Steps:
            * 1. Setup supplier account
            * 2. Create supplier contract
            * 3. Create contact record (physician)
            * 4. Add contact to supplier contract relationship
            * 5. Verify the relationship was created
            */

            // Step 1: Setup supplier
            var supplier = await _accounts.SetupSupplierAsync();
            Assert.IsNotNull(supplier, "Supplier account should be created.");
            TestContext.WriteLine($"Created Supplier with ID: {supplier}");

            // Step 2: Create supplier contract
            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: DateTime.UtcNow.Date,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = false,
                    AllPhysicians = true,
                }
            );
            Assert.IsNotNull(supplierContractId, "Supplier Contract should be created.");
            TestContext.WriteLine($"Created Supplier Contract with ID: {supplierContractId}");

          // Create a DIFFERENT hospital for the rule (Hospital2)
            var hospital2 = await _accounts.SetupHospitalAsync();
            Assert.IsNotNull(hospital2, "Hospital 2 record should exist after setup.");
            TestContext.WriteLine($"Created Hospital 2 with ID: {hospital2} (different from case hospital)");

            // Step 4: Add contact to supplier contract relationship
            await Client.AddReferenceAsync(
                sourceEntityLogicalName: EntityNames.SupplierContract,
                sourceId: supplierContractId,
                navigationPropertyName: "leap_leap_suppliercontract_account",  
                targetEntityLogicalName: EntityNames.Account,
                targetId: hospital2
            );
            TestContext.WriteLine(
                $"Added Contact {hospital2} to Supplier Contract {supplierContractId} relationship."
            );

            // Step 5: Verify the relationship was created by retrieving the supplier contract
            var supplierContractRecord = await Client.GetAsync(
                EntityNames.SupplierContract,
                supplierContractId
            );
            Assert.IsNotNull(supplierContractRecord, "Supplier Contract record should exist.");
            TestContext.WriteLine(
                "✓ Supplier Contract with Contact relationship successfully created and verified."
            );
                              
            var procedure = Seed.ProcedureId;
            var physician = Seed.PhysicianId;
            var caseRecord = await _cases.SetupAsync(hospital2, physician, procedure);

              // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );            

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital2,
                    supplier,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

             // Create supplier commission rule with populated HospitalId matching the case's hospital
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Hospital Match {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveStartDate = DateTime.UtcNow.Date,
                    HospitalId = hospital2,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with HospitalId: {hospital2} (matching case hospital)"
            );

             // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                1,
                commissions.Count,
                "When rule has non-empty physician field matching case physician and contract has AllPhysicians=false, should generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly generated for matching physician rule."
            );

        }

         [TestMethod]
        public async Task SupplierContract_ContractWithHospitalinListButNotMatch_ShouldNotGenerateCommission()
        {
            /**
            * Goal: Create a supplier contract, then create a contact record called "physician", 
            * and add the contact to the leap_leap_suppliercontract_contact entity relationship.
            * 
            * Steps:
            * 1. Setup supplier account
            * 2. Create supplier contract
            * 3. Create contact record (physician)
            * 4. Add contact to supplier contract relationship
            * 5. Verify the relationship was created
            */

            // Step 1: Setup supplier
            var supplier = await _accounts.SetupSupplierAsync();
            Assert.IsNotNull(supplier, "Supplier account should be created.");
            TestContext.WriteLine($"Created Supplier with ID: {supplier}");

            // Step 2: Create supplier contract
            var supplierContractId = await _supplierContracts.SetupAsync(
                SupplierContractConfig.Default(
                    name: $"Test Contract {Config.CorrelationId}",
                    supplierId: supplier,
                    entityId: supplier,
                    effectiveStartDate: DateTime.UtcNow.Date,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    AllHospitals = false,
                    AllPhysicians = true,
                }
            );
            Assert.IsNotNull(supplierContractId, "Supplier Contract should be created.");
            TestContext.WriteLine($"Created Supplier Contract with ID: {supplierContractId}");

          // Create a DIFFERENT hospital for the rule (Hospital2)
            var hospital2 = await _accounts.SetupHospitalAsync();
            Assert.IsNotNull(hospital2, "Hospital 2 record should exist after setup.");
            TestContext.WriteLine($"Created Hospital 2 with ID: {hospital2} (different from case hospital rule)");

            // Step 4: Add contact to supplier contract relationship
            await Client.AddReferenceAsync(
                sourceEntityLogicalName: EntityNames.SupplierContract,
                sourceId: supplierContractId,
                navigationPropertyName: "leap_leap_suppliercontract_account",  
                targetEntityLogicalName: EntityNames.Account,
                targetId: hospital2
            );
            TestContext.WriteLine(
                $"Added Contact {hospital2} to Supplier Contract {supplierContractId} relationship."
            );

            // Step 5: Verify the relationship was created by retrieving the supplier contract
            var supplierContractRecord = await Client.GetAsync(
                EntityNames.SupplierContract,
                supplierContractId
            );
            Assert.IsNotNull(supplierContractRecord, "Supplier Contract record should exist.");
            TestContext.WriteLine(
                "✓ Supplier Contract with Contact relationship successfully created and verified."
            );

            var hospitalinit = Seed.HospitalId;                  
            var procedure = Seed.ProcedureId;
            var physician = Seed.PhysicianId;
            var caseRecord = await _cases.SetupAsync(hospital2, physician, procedure);

              // Setup products and case products
            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var manufacturer = await _accounts.SetupManufacturerAsync(
                OptionSetValues.Account.SalesCategory.Woundcare
            );            

            var productFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                productFamily,
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 100.00m
            );

            var caseProductId = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital2,
                    supplier,
                    manufacturer,
                    productFamily,
                    product
                ) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                caseProductId,
                "Case Product record should exist after setup."
            );

             // Create supplier commission rule with populated HospitalId matching the case's hospital
            var ruleId = await _supplierCommissionRules.SetupAsync(
                SupplierCommissionRuleConfig.Default(
                    name: $"Test Rule Hospital Match {Config.CorrelationId}",
                    supplierContractId: supplierContractId,
                    ownerId: Config.OwnerUserId
                ) with
                {
                    EffectiveStartDate = DateTime.UtcNow.Date,
                    HospitalId = hospitalinit,
                    CommissionPercent = 10.0m,
                }
            );

            Assert.IsNotNull(ruleId, "Supplier Commission Rule record should exist after setup.");

            TestContext.WriteLine(
                $"Supplier Commission Rule created with HospitalId: {hospitalinit} (Donot matching case use hospital2)"
            );

             // Trigger case regeneration
            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(5)
            );

            TestContext.WriteLine("Case regeneration completed");

            // Verify commission was generated
            var commissions = await _supplierCommissions.GetByCaseAndRuleAsync(
                caseRecord,
                ruleId
            );

            Assert.AreEqual(
                1,
                commissions.Count,
                "When rule has non-empty physician field matching case physician and contract has AllPhysicians=false, should generate supplier commission."
            );

            TestContext.WriteLine(
                "✓ Supplier Commission correctly generated for matching physician rule."
            );

        }


    }
}

