using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Fixtures;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace E2E.Api.Tests.Tests
{
    [TestClass]
    public class GenerateUsageFormsTest : BaseTest
    {
        public TestContext TestContext { get; set; } = null!;

        private AccountFixture _accounts = null!;
        private CaseFixture _cases = null!;
        private ProductFixture _products = null!;
        private CaseProductFixture _caseProducts = null!;
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
        public async Task GenerateUsageForms_ShouldTakeUsageFormAndCaseConfigurationIntoAccount()
        {
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

            var organizationId = Seed.OrganizationId;
            var subjectId = Seed.SubjectId;
            var unitGroupId = Seed.UnitGroupId;
            var unitId = Seed.UnitId;
            var supplier = await _accounts.SetupSupplierAsync();

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
                OptionSetValues.Product.ProductType.Consignment,
                costPrice: 150.00m
            );

            Assert.IsNotNull(sbProduct, "Stock & Bill product must be created.");
            Assert.IsNotNull(consignmentProduct, "Consignment product must be created.");

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
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m
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
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = true,
                    UnitPrice = 250.00m
                }
            );

            Assert.IsNotNull(sbCaseProduct, "Stock & Bill case product must be created.");
            Assert.IsNotNull(consignmentCaseProduct, "Consignment case product must be created.");

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(
                caseRecord,
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(2)
            );

            TestContext.WriteLine("Case regeneration completed and usage forms processed.");

            var usageFormRecords = await _usageForms.GetByCaseAsync(caseRecord);
            Assert.AreEqual(
                4,
                usageFormRecords.Count,
                "Expected exactly 4 Usage Forms to be generated for the configured case."
            );

            var usageFormIds = usageFormRecords
                .Select(uf => uf.GetProperty("leap_usageformid").GetGuid())
                .ToList();

            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseRecord);
            Assert.AreEqual(
                4,
                usageFormDetails.Count,
                "Expected exactly 4 Usage Form Details for the configured case."
            );

            var distinctUsageFormIds = usageFormDetails
                .Select(detail =>
                {
                    Assert.IsTrue(
                        detail.TryGetProperty("leap_usageform", out var formIdProperty),
                        "Usage Form Detail should contain a usage form lookup."
                    );
                    return formIdProperty.GetGuid();
                })
                .Distinct()
                .ToList();

            CollectionAssert.AreEquivalent(
                usageFormIds,
                distinctUsageFormIds,
                "Each generated Usage Form should have matching Usage Form Detail records."
            );

            var stockAndBillDetails = usageFormDetails
                .Where(detail =>
                    detail.TryGetProperty("leap_hospitalowned", out var hospitalOwned) &&
                    hospitalOwned.ValueKind == JsonValueKind.False
                )
                .ToList();

            var hospitalOwnedDetails = usageFormDetails
                .Where(detail =>
                    detail.TryGetProperty("leap_hospitalowned", out var hospitalOwned) &&
                    hospitalOwned.ValueKind == JsonValueKind.True
                )
                .ToList();

            Assert.AreEqual(
                1,
                stockAndBillDetails.Count,
                "Expected one Stock & Bill Usage Form Detail because only one non-hospital-owned case product was configured."
            );
            Assert.AreEqual(
                3,
                hospitalOwnedDetails.Count,
                "Expected three hospital-owned Usage Form Details because the consignment case product quantity is three."
            );

            ValidateUsageFormDetailReferences(stockAndBillDetails, sbCaseProduct);
            ValidateUsageFormDetailReferences(hospitalOwnedDetails, consignmentCaseProduct);

            TestContext.WriteLine("Usage form configuration assertions passed.");
        }

        private static void ValidateUsageFormDetailReferences(
            List<JsonElement> details,
            Guid expectedCaseProductId
        )
        {
            foreach (var detail in details)
            {
                Assert.IsTrue(
                    detail.TryGetProperty("_leap_caseproduct_value", out var caseProductProperty),
                    "Usage Form Detail must reference a case product."
                );

                Assert.AreEqual(
                    expectedCaseProductId,
                    caseProductProperty.GetGuid(),
                    "Usage Form Detail should reference the configured case product."
                );
            }
        }

        [TestMethod]
        public async Task Should_Generate_HO_UF_UFD_When_HO_UF_Count_Is_Greater_Than_Zero()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            var supplier = await _accounts.SetupSupplierAsync();
            var productFamily = await _products.SetupProductFamilyAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, OptionSetValues.Product.ProductType.Consignment);
            var product = await _products.SetupProductAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, productFamily, OptionSetValues.Product.ProductType.Consignment);

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, supplier, productFamily, product) with
                {
                    Quantity = 2,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = true,
                    UnitPrice = 100.00m
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord);

            var details = await _usageFormDetails.GetByCaseAsync(caseRecord);
            var hospitalOwnedDetails = details.Where(d => d.GetProperty("leap_hospitalowned").GetBoolean()).ToList();

            Assert.IsTrue(hospitalOwnedDetails.Count > 0, "Hospital owned Usage Form Details should be generated when HO UF count > 0.");
            foreach (var d in hospitalOwnedDetails)
                Assert.AreEqual(caseProduct, d.GetProperty("_leap_caseproduct_value").GetGuid());
        }

        [TestMethod]
        public async Task Should_Not_Generate_HO_UF_UFD_When_HO_UF_Count_Is_Zero()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            var supplier = await _accounts.SetupSupplierAsync();
            var productFamily = await _products.SetupProductFamilyAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, OptionSetValues.Product.ProductType.Consignment);
            var product = await _products.SetupProductAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, productFamily, OptionSetValues.Product.ProductType.Consignment);

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, supplier, productFamily, product) with
                {
                    Quantity = 0,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = true,
                    UnitPrice = 100.00m
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord);

            var details = await _usageFormDetails.GetByCaseAsync(caseRecord);
            var hospitalOwnedDetails = details.Where(d => d.GetProperty("leap_hospitalowned").GetBoolean()).ToList();

            Assert.AreEqual(0, hospitalOwnedDetails.Count, "No hospital-owned usage form details should be generated when count is zero.");
        }

        [TestMethod]
        public async Task Should_Generate_NonIO_UsageFormDetail_With_New_Product_For_HospitalOwned_Case()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            var supplier = await _accounts.SetupSupplierAsync();
            var productFamily = await _products.SetupProductFamilyAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, OptionSetValues.Product.ProductType.Consignment);
            var newProduct = await _products.SetupProductAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, productFamily, OptionSetValues.Product.ProductType.Consignment);

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, supplier, productFamily, newProduct) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = true,
                    IsIO = false,
                    UnitPrice = 120.00m
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord);

            var details = await _usageFormDetails.GetByCaseAsync(caseRecord);
            var nonIo = details.Where(d => d.GetProperty("leap_hospitalowned").GetBoolean() && d.GetProperty("leap_io").GetBoolean() == false).ToList();

            Assert.IsTrue(nonIo.Count == 1, "A non-I/O Usage Form Detail should be created for a new product in HO case.");
            Assert.AreEqual(newProduct, nonIo[0].GetProperty("_leap_caseproduct_value").GetGuid());
        }

        [TestMethod]
        public async Task Should_Generate_NonIO_UsageFormDetail_With_Existing_Product_For_HospitalOwned_Case()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            var supplier = await _accounts.SetupSupplierAsync();
            var productFamily = await _products.SetupProductFamilyAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, OptionSetValues.Product.ProductType.Consignment);
            var existingProduct = await _products.SetupProductAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, productFamily, OptionSetValues.Product.ProductType.Consignment);

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, supplier, productFamily, existingProduct) with
                {
                    Quantity = 1,
                    ProductType = OptionSetValues.CaseProduct.ProductType.Consignment,
                    IsHospitalOwned = true,
                    IsIO = false,
                    UnitPrice = 130.00m
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord);

            var details = await _usageFormDetails.GetByCaseAsync(caseRecord);
            var nonIo = details.Where(d => d.GetProperty("leap_hospitalowned").GetBoolean() && d.GetProperty("leap_io").GetBoolean() == false).ToList();

            Assert.IsTrue(nonIo.Count == 1, "A non-I/O Usage Form Detail should be created for existing product in HO case.");
            Assert.AreEqual(existingProduct, nonIo[0].GetProperty("_leap_caseproduct_value").GetGuid());
        }

        [TestMethod]
        public async Task Should_Generate_IO_And_NonIO_UsageFormDetails_When_CP_Quantity_Is_Greater_Than_CP_IO_Quantity_And_Product_Is_Existing()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            var supplier = await _accounts.SetupSupplierAsync();
            var productFamily = await _products.SetupProductFamilyAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, OptionSetValues.Product.ProductType.StockAndBill);
            var existingProduct = await _products.SetupProductAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, productFamily, OptionSetValues.Product.ProductType.StockAndBill);

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, supplier, productFamily, existingProduct) with
                {
                    Quantity = 5,
                    IsIO = true,
                    IOQuantity = 2,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                    IOUnitPrice = 200.00m
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord);

            var details = await _usageFormDetails.GetByCaseAsync(caseRecord);
            var ioDetails = details.Where(d => d.GetProperty("leap_io").GetBoolean()).ToList();
            var nonIoDetails = details.Where(d => !d.GetProperty("leap_io").GetBoolean()).ToList();

            Assert.IsTrue(ioDetails.Count >= 1, "Expected at least one I/O detail.");
            Assert.IsTrue(nonIoDetails.Count >= 1, "Expected at least one non-I/O detail.");

            var ioQty = ioDetails.Sum(d => d.GetProperty("leap_qty").GetInt32());
            var nonIoQty = nonIoDetails.Sum(d => d.GetProperty("leap_qty").GetInt32());
            Assert.AreEqual(2, ioQty, "I/O quantity should equal CP IO quantity.");
            Assert.AreEqual(3, nonIoQty, "Non-I/O quantity should equal CP Quantity - IO Quantity.");
        }

        [TestMethod]
        public async Task Should_Generate_Only_IO_UsageFormDetails_When_CP_Quantity_Is_Less_Than_Or_Equal_To_CP_IO_Quantity_And_Product_Is_Existing()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            var supplier = await _accounts.SetupSupplierAsync();
            var productFamily = await _products.SetupProductFamilyAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, OptionSetValues.Product.ProductType.StockAndBill);
            var existingProduct = await _products.SetupProductAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, productFamily, OptionSetValues.Product.ProductType.StockAndBill);

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, supplier, productFamily, existingProduct) with
                {
                    Quantity = 2,
                    IsIO = true,
                    IOQuantity = 3,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 200.00m,
                    IOUnitPrice = 200.00m
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord);

            var details = await _usageFormDetails.GetByCaseAsync(caseRecord);
            var ioDetails = details.Where(d => d.GetProperty("leap_io").GetBoolean()).ToList();
            var nonIoDetails = details.Where(d => !d.GetProperty("leap_io").GetBoolean()).ToList();

            Assert.IsTrue(ioDetails.Count >= 1, "Expected I/O details to exist.");
            Assert.AreEqual(0, nonIoDetails.Count, "No non-I/O details should be created when CP Quantity <= IO Quantity.");

            var ioQty = ioDetails.Sum(d => d.GetProperty("leap_qty").GetInt32());
            Assert.AreEqual(2, ioQty, "I/O quantity should equal CP Quantity.");
        }

        [TestMethod]
        public async Task Should_Generate_IO_And_NonIO_UsageFormDetails_When_CP_Quantity_Is_Greater_Than_CP_IO_Quantity_And_Product_Is_New()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            var supplier = await _accounts.SetupSupplierAsync();
            var productFamily = await _products.SetupProductFamilyAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, OptionSetValues.Product.ProductType.StockAndBill);
            var newProduct = await _products.SetupProductAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, productFamily, OptionSetValues.Product.ProductType.StockAndBill);

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, supplier, productFamily, newProduct) with
                {
                    Quantity = 5,
                    IsIO = true,
                    IOQuantity = 2,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 180.00m,
                    IOUnitPrice = 180.00m
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord);

            var details = await _usageFormDetails.GetByCaseAsync(caseRecord);
            var ioDetails = details.Where(d => d.GetProperty("leap_io").GetBoolean()).ToList();
            var nonIoDetails = details.Where(d => !d.GetProperty("leap_io").GetBoolean()).ToList();

            Assert.IsTrue(ioDetails.Count >= 1, "Expected at least one I/O detail for new product.");
            Assert.IsTrue(nonIoDetails.Count >= 1, "Expected at least one non-I/O detail for new product.");

            var ioQty = ioDetails.Sum(d => d.GetProperty("leap_qty").GetInt32());
            var nonIoQty = nonIoDetails.Sum(d => d.GetProperty("leap_qty").GetInt32());
            Assert.AreEqual(2, ioQty, "I/O quantity should equal CP IO quantity.");
            Assert.AreEqual(3, nonIoQty, "Non-I/O quantity should equal CP Quantity - IO Quantity.");
        }

        [TestMethod]
        public async Task Should_Generate_Only_IO_UsageFormDetails_When_CP_Quantity_Is_Less_Than_Or_Equal_To_CP_IO_Quantity_And_Product_Is_New()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            var supplier = await _accounts.SetupSupplierAsync();
            var productFamily = await _products.SetupProductFamilyAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, OptionSetValues.Product.ProductType.StockAndBill);
            var newProduct = await _products.SetupProductAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, productFamily, OptionSetValues.Product.ProductType.StockAndBill);

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, supplier, productFamily, newProduct) with
                {
                    Quantity = 2,
                    IsIO = true,
                    IOQuantity = 3,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 180.00m,
                    IOUnitPrice = 180.00m
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord);

            var details = await _usageFormDetails.GetByCaseAsync(caseRecord);
            var ioDetails = details.Where(d => d.GetProperty("leap_io").GetBoolean()).ToList();
            var nonIoDetails = details.Where(d => !d.GetProperty("leap_io").GetBoolean()).ToList();

            Assert.IsTrue(ioDetails.Count >= 1, "Expected I/O details to exist for new product.");
            Assert.AreEqual(0, nonIoDetails.Count, "Expected no non-I/O details when CP Quantity <= IO Quantity for new product.");
        }

        [TestMethod]
        public async Task Should_Generate_UF_UFD_For_NonHospitalOwned_When_UF_Count_Is_Greater_Than_Zero()
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var caseRecord = await _cases.SetupAsync(hospital, physician, procedure);

            var supplier = await _accounts.SetupSupplierAsync();
            var productFamily = await _products.SetupProductFamilyAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, OptionSetValues.Product.ProductType.StockAndBill);
            var product = await _products.SetupProductAsync(Seed.OrganizationId, Seed.SubjectId, Seed.UnitGroupId, Seed.UnitId, supplier, productFamily, OptionSetValues.Product.ProductType.StockAndBill);

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, supplier, supplier, productFamily, product) with
                {
                    Quantity = 2,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = 220.00m
                }
            );

            await _cases.RegenCaseAsync(caseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseRecord);

            var ufs = await _usageForms.GetByCaseAsync(caseRecord);
            var ufd = await _usageFormDetails.GetByCaseAsync(caseRecord);

            Assert.IsTrue(ufs.Count > 0, "Usage Forms should be generated for non-hospital-owned products when UF count > 0.");
            Assert.IsTrue(ufd.Count > 0, "Usage Form Details should be generated for non-hospital-owned products when UF count > 0.");
            CollectionAssert.IsSubsetOf(ufs.Select(u => u.GetProperty("leap_usageformid").GetGuid()).ToList(), ufd.Select(d => d.GetProperty("leap_usageform").GetGuid()).Distinct().ToList());
        }
    }
}
