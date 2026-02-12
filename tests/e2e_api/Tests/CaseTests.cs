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
    public class CaseTests : BaseTest
    {
        public TestContext TestContext { get; set; } = null!;

        // Fixtures
        private AccountFixture _accounts = null!;
        private CaseFixture _cases = null!;
        private ProductFixture _products = null!;
        private CaseProductFixture _caseProducts = null!;

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
            if (_products != null)
                await _products.CleanupAsync();
            if (_accounts != null)
                await _accounts.CleanupAsync();
        }

        [TestMethod]
        public async Task Case_Should_Generate_Correct_Usage_Forms()
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
            // product setup
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

            // consignment product
            var consignmentProductFamily = await _products.SetupProductFamilyAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                OptionSetValues.Product.ProductType.Consignment
            );
            var consignmentProduct = await _products.SetupProductAsync(
                organizationId,
                subjectId,
                unitGroupId,
                unitId,
                supplier,
                consignmentProductFamily,
                OptionSetValues.Product.ProductType.Consignment
            );

            Assert.IsNotNull(sbProduct, "Stock&Bill Product record should exist after setup.");
            Assert.IsNotNull(
                consignmentProduct,
                "Consignment Product record should exist after setup."
            );

            // case products setup
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
                    Quantity = 5,
                    ProductType = OptionSetValues.CaseProductConfig.ProductType.StockAndBill,
                    UnitPrice = 150.00m,
                }
            );

            var consignmentCaseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(
                    caseRecord,
                    hospital,
                    supplier,
                    supplier,
                    consignmentProductFamily,
                    consignmentProduct
                ) with
                {
                    Quantity = 3,
                    ProductType = OptionSetValues.CaseProductConfig.ProductType.Consignment,
                    IsHospitalOwned = true,
                    UnitPrice = 200.00m,
                }
            );

            Assert.IsNotNull(
                sbCaseProduct,
                "Stock&Bill Case Product record should exist after setup."
            );
            Assert.IsNotNull(
                consignmentCaseProduct,
                "Consignment Case Product record should exist after setup."
            );

            /**
            * step 3: trigger case regeneration and await completion
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
            * step 4: validate usage forms and usage form details
            */

            // validate usage forms
            var usageFormRecords = await _usageForms.GetBySupplierAsync(caseRecord, supplier);
            Assert.AreEqual(
                4,
                usageFormRecords.Count,
                "There should be exactly 4 Usage Forms generated for the case and supplier."
            );

            var sbUfs = usageFormRecords
                .Where(uf => uf.GetProperty("leap_hospitalowned").GetBoolean() == false)
                .ToList();
            Assert.AreEqual(1, sbUfs.Count, "Expected exactly 1 Stock & Bill Usage Form.");

            var consignmentUfs = usageFormRecords
                .Where(uf => uf.GetProperty("leap_hospitalowned").GetBoolean() == true)
                .ToList();
            Assert.AreEqual(3, consignmentUfs.Count, "Expected exactly 3 Consignment Usage Form.");

            // validate Stock&Bill Usage Form details
            var sbUsageFormDetails = await _usageFormDetails.GetByUsageFormAsync(
                sbUfs[0].GetProperty("leap_usageformid").GetGuid()
            );
            Assert.AreEqual(
                1,
                sbUsageFormDetails.Count,
                "There should be exactly 1 Usage Form Detail for the Stock&Bill Usage Form."
            );
            Assert.AreEqual(
                sbCaseProduct,
                sbUsageFormDetails[0].GetProperty("_leap_caseproduct_value").GetGuid(),
                "The Stock&Bill Usage Form Detail should reference the correct Case Product."
            );

            // validate Consignment Usage Form details
            foreach (var uf in consignmentUfs)
            {
                var consignmentUsageFormDetails = await _usageFormDetails.GetByUsageFormAsync(
                    uf.GetProperty("leap_usageformid").GetGuid()
                );

                Assert.AreEqual(
                    1,
                    consignmentUsageFormDetails.Count,
                    "There should be exactly 1 Usage Form Detail for the HO Consignment Usage Form."
                );
                Assert.AreEqual(
                    consignmentCaseProduct,
                    consignmentUsageFormDetails[0].GetProperty("_leap_caseproduct_value").GetGuid(),
                    "The Consignment Usage Form Detail should reference the correct Case Product."
                );
            }

            TestContext.WriteLine($"Assertions completed for Case ID: {caseRecord}");
        }
    }
}
