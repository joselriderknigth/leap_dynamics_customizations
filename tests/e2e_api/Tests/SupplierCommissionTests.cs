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

        // Services
        private UsageFormService _usageForms = null!;
        private UsageFormDetailService _usageFormDetails = null!;

        [TestInitialize]
        public void Setup()
        {
            _accounts = new AccountFixture(Client, Config);
            _cases = new CaseFixture(Client, Config);
            _products = new ProductFixture(Client, Config);
            _caseProducts = new CaseProductFixture(Client, Config);
            _supplierContracts = new SupplierContractFixture(Client, Config);
            _supplierCommissionRules = new SupplierCommissionRuleFixture(Client, Config);

            _usageForms = new UsageFormService(Client);
            _usageFormDetails = new UsageFormDetailService(Client);
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
                    ProductType = OptionSetValues.CaseProductConfig.ProductType.StockAndBill,
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
                    ProductType = OptionSetValues.CaseProductConfig.ProductType.Consignment,
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
                    ProductType = OptionSetValues.CaseProductConfig.ProductType.Consignment,
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
                    ProductType = OptionSetValues.CaseProductConfig.ProductType.Consignment,
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
    }
}
