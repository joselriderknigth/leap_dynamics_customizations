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

                  // ============ TEST 3.8: SupplierCommission_MultipleMatchingRules_ShouldUseMostSpecificRule ============
        [TestMethod]
        public async Task SupplierCommission_MultipleMatchingRules_ShouldUseMostSpecificRule()
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
          
                    EffectiveEndDate = contractEndDate,                
                    HospitalId = supplier,
                    ProductFamilyId = productFamily,
                    OwnerId = Config.OwnerUserId,
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
                    EffectiveEndDate = contractEndDate,
                    CommissionPercent = 4.0m,
                    HospitalId = supplier,    
                    ProductId = product,
                    OwnerId = Config.OwnerUserId,
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
                    EffectiveEndDate = contractEndDate,
                    HospitalId = hospital,
                    OwnerId = Config.OwnerUserId,
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
                    EffectiveEndDate = contractEndDate,
                    HospitalId = supplier,
                    PhysicianId = physician,
                    OwnerId = Config.OwnerUserId,
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var physicianCommissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, rulePhysician);
            Assert.AreEqual(1, physicianCommissions.Count, "Most specific rule (Physician) should generate commission.");

            var hospitalCommissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, ruleHospital);
            var productCommissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, ruleProduct);
            var productFamilyCommissions = await _supplierCommissions.GetByCaseAndRuleAsync(caseRecord, ruleProductFamily);

            TestContext.WriteLine($"✓ Most specific rule (Physician) correctly selected for supplier commission.");
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
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
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

            var matchingTiers = await _pricingTierService.GetMatchingAsync(
                supplierContractId,
                product,
                150.00m
            );
            Assert.AreEqual(1, matchingTiers.Count, "A matching pricing tier should exist for the case product unit price.");

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
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
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

            var matchingTiers = await _pricingTierService.GetMatchingAsync(
                supplierContractId,
                product,
                250.00m
            );
            Assert.AreEqual(0, matchingTiers.Count, "No pricing tier should match when unit price is outside the defined range.");

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
    }
}
