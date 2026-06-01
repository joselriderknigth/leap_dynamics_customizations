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
    [TestCategory("ManagerStandard")]
    public class ManagerStandardCommissionTests : BaseSalesRepCommissionTest
    {
        public TestContext TestContext { get; set; } = null!;

        private AccountFixture _accounts = null!;
        private CaseFixture _cases = null!;
        private ProductFixture _products = null!;
        private CaseProductFixture _caseProducts = null!;
        private SalesRepContractFixture _salesRepContracts = null!;
        private SalesRepCommissionRuleFixture _salesRepCommissionRules = null!;

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

        private async Task<(Guid CaseRecord, Guid Hospital, Guid Physician, Guid Procedure, Guid Manufacturer, Guid ProductFamily, Guid Product, decimal UnitPrice, int Quantity, decimal CostPrice)> SetupManagerStandardCaseAsync(
            DateTime caseDateTime,
            decimal costPrice = 100.00m,
            decimal unitPrice = 200.00m,
            int quantity = 1
        )
        {
            var hospital = Seed.HospitalId;
            var physician = Seed.PhysicianId;
            var procedure = Seed.ProcedureId;
            var salesCategory = OptionSetValues.Account.SalesCategory.Woundcare;

            var caseRecord = await _cases.SetupAsync(
                hospital,
                physician,
                procedure,
                $"PAT-{Guid.NewGuid()}",
                caseDateTime
            );

            Assert.IsNotNull(caseRecord, "Case record should exist after setup.");

            var manufacturer = await _accounts.SetupManufacturerAsync(salesCategory);
            Assert.IsNotNull(manufacturer, "Manufacturer account should be created.");

            var productFamily = await _products.SetupProductFamilyAsync(
                Seed.OrganizationId,
                Seed.SubjectId,
                Seed.UnitGroupId,
                Seed.UnitId,
                manufacturer,
                OptionSetValues.Product.ProductType.StockAndBill
            );

            var product = await _products.SetupProductAsync(
                Seed.OrganizationId,
                Seed.SubjectId,
                Seed.UnitGroupId,
                Seed.UnitId,
                manufacturer,
                productFamily,
                OptionSetValues.Product.ProductType.StockAndBill,
                costPrice: costPrice
            );

            Assert.IsNotNull(product, "Product record should exist after setup.");

            var caseProduct = await _caseProducts.SetupAsync(
                CaseProductConfig.Default(caseRecord, hospital, manufacturer, manufacturer, productFamily, product) with
                {
                    Quantity = quantity,
                    ProductType = OptionSetValues.CaseProduct.ProductType.StockAndBill,
                    IsHospitalOwned = false,
                    UnitPrice = unitPrice,
                }
            );

            Assert.IsNotNull(caseProduct, "Case Product record should exist after setup.");

            return (
                CaseRecord: caseRecord,
                Hospital: hospital,
                Physician: physician,
                Procedure: procedure,
                Manufacturer: manufacturer,
                ProductFamily: productFamily,
                Product: product,
                UnitPrice: unitPrice,
                Quantity: quantity,
                CostPrice: costPrice
            );
        }

        private async Task<Guid> CreateManagerStandardContractAsync(
            Guid ownerId,
            Guid salesRepId,
            int repRole,
            DateTime contractStartDate,
            DateTime? contractEndDate
        )
        {
            return await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: ownerId
                ) with
                {
                    Name = $"Manager Standard Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = salesRepId,
                    RepRole = repRole,
                }
            );
        }

        private async Task<Guid> CreateManagerStandardRuleAsync(
            Guid ownerId,
            Guid contractId,
            DateTime ruleStartDate,
            DateTime? ruleEndDate,
            Guid? businessUnitId = null,
            bool? isOwnedByTeam = null,
            Guid? salesRepId = null,
            decimal commissionSplit = 100m,
            int salesCategory = OptionSetValues.Account.SalesCategory.Woundcare
        )
        {
            return await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.ManagerStandard,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: commissionSplit,
                    startDate: ruleStartDate,
                    ownerId: ownerId,
                    salesRepContractId: contractId
                ) with
                {
                    Name = $"Manager Standard Rule {Config.CorrelationId}",
                    EndDate = ruleEndDate,
                    SalesCategory = salesCategory,
                    SalesRepId = salesRepId,
                    BusinessUnitId = businessUnitId,
                    IsOwnedByTeam = isOwnedByTeam,
                }
            );
        }

        private async Task<Guid> CreateStandaloneCommissionRuleAsync(
            Guid ownerId,
            Guid salesRepId,
            DateTime contractStartDate,
            DateTime? contractEndDate,
            bool isOwnedByTeam = false
        )
        {
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 3m,
                    maxCommissionRate: 7m,
                    startDate: contractStartDate,
                    ownerId: ownerId
                ) with
                {
                    Name = $"Standalone Sales Rep Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = salesRepId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.SR,
                    IsOwnedByTeam = isOwnedByTeam,
                }
            );

            return await _salesRepCommissionRules.SetupAsync(
                SalesRepCommissionRuleConfig.Default(
                    commissionRuleType: OptionSetValues.SalesRepCommissionRule.CommissionType.Standalone,
                    statusCode: Config.DefaultStatusCode,
                    commissionSplit: 100m,
                    startDate: contractStartDate,
                    ownerId: ownerId,
                    salesRepContractId: salesRepContractId
                ) with
                {
                    Name = $"Standalone Rule {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = salesRepId,
                    RequiredSalesRepMatch = false,
                    BlockVPstandardCommission = false,
                    IsOwnedByTeam = isOwnedByTeam,
                }
            );
        }

        [TestMethod]
        public async Task ManagerStandardCommissionHappyPath()
        {
            var caseDateTime = DateTime.UtcNow;
            var caseSetup = await SetupManagerStandardCaseAsync(caseDateTime);

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await CreateManagerStandardContractAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                repRole: OptionSetValues.SalesRepContract.RepRole.Manager,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var standaloneRuleId = await CreateStandaloneCommissionRuleAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Config.OwnerUserId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                salesRepId: Config.OwnerUserId,
                commissionSplit: 100m
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "A prior Standalone commission should exist before Manager Standard commission is generated.");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Exactly 1 Manager Standard Sales Rep Commission should be created.");

            var commission = salesRepCommissions[0];

            ValidateCommissionRelationships(
                commission: commission,
                expectedContractId: salesRepContractId,
                expectedRuleId: managerRuleId,
                testContext: TestContext
            );

            commission.TryGetProperty("leap_commissionsplit", out var commissionSplitValue);
            Assert.AreEqual(100m, commissionSplitValue.GetDecimal(), "Commission Split should match the Manager Standard rule configuration.");

            ValidateCommissionRateAndAmount(
                commission: commission,
                expectedRate: 10m,
                unitPrice: caseSetup.UnitPrice,
                quantity: caseSetup.Quantity,
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
        }

        [TestMethod]
        public async Task ManagerStandardCommission_OwnerTeamBusinessUnitMatch()
        {
            var caseDateTime = DateTime.UtcNow;
            var caseSetup = await SetupManagerStandardCaseAsync(caseDateTime);

            var team = await _teams.GetAsync(Seed.TeamId);
            team.TryGetProperty("_businessunitid_value", out var teamBusinessUnitId);
            Assert.IsNotNull(teamBusinessUnitId, "Team should have a business unit.");

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Seed.TeamId
                ) with
                {
                    Name = $"Manager Standard Team Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.Manager,
                    IsOwnedByTeam = true,
                }
            );

            var standaloneRuleId = await CreateStandaloneCommissionRuleAsync(
                ownerId: Seed.TeamId,
                salesRepId: Config.OwnerUserId,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate,
                isOwnedByTeam: true
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Seed.TeamId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                businessUnitId: teamBusinessUnitId.GetGuid(),
                isOwnedByTeam: true,
                salesRepId: Config.OwnerUserId
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "A prior Standalone commission should exist before Manager Standard commission is generated.");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Exactly 1 Manager Standard Sales Rep Commission should be created for team-owned rule.");

            var commission = salesRepCommissions[0];

            ValidateCommissionOwnership(
                commission: commission,
                expectedOwnerId: Seed.TeamId,
                expectedOwningBusinessUnitId: teamBusinessUnitId.GetGuid(),
                expectedSalesRepId: null,
                expectedExternalAgentId: null,
                expectedDistributorId: null,
                testContext: TestContext
            );
        }

        [TestMethod]
        public async Task ManagerStandardCommission_OwnerIdMatch()
        {
            var caseDateTime = DateTime.UtcNow;
            var caseSetup = await SetupManagerStandardCaseAsync(caseDateTime);

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await CreateManagerStandardContractAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                repRole: OptionSetValues.SalesRepContract.RepRole.Manager,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var standaloneRuleId = await CreateStandaloneCommissionRuleAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Config.OwnerUserId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                salesRepId: Config.OwnerUserId
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "A prior Standalone commission should exist before Manager Standard commission is generated.");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Exactly 1 Manager Standard Sales Rep Commission should be created for owner-matched rule.");

            var commission = salesRepCommissions[0];

            var systemUser = await _systemUsers.GetAsync(Config.OwnerUserId);
            systemUser.TryGetProperty("_businessunitid_value", out var ownerBusinessUnitId);

            ValidateCommissionOwnership(
                commission: commission,
                expectedOwnerId: Config.OwnerUserId,
                expectedOwningBusinessUnitId: ownerBusinessUnitId.GetGuid(),
                expectedSalesRepId: Config.OwnerUserId,
                expectedExternalAgentId: null,
                expectedDistributorId: null,
                testContext: TestContext
            );
        }

        [TestMethod]
        public async Task ManagerStandardCommission_OwnerIdMismatch_NoCommissionGenerated()
        {
            var caseDateTime = DateTime.UtcNow;
            var caseSetup = await SetupManagerStandardCaseAsync(caseDateTime);

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await CreateManagerStandardContractAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                repRole: OptionSetValues.SalesRepContract.RepRole.Manager,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var standaloneRuleId = await CreateStandaloneCommissionRuleAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Seed.TeamId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                salesRepId: Config.OwnerUserId
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "A prior Standalone commission should exist before Manager Standard commission is generated.");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, "No commission should be created when rule owner does not match contract owner.");
        }

        [TestMethod]
        public async Task ManagerStandardCommission_NoPreviousCommission_NoManagerCommissionGenerated()
        {
            var caseDateTime = DateTime.UtcNow;
            var caseSetup = await SetupManagerStandardCaseAsync(caseDateTime);

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await CreateManagerStandardContractAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                repRole: OptionSetValues.SalesRepContract.RepRole.Manager,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Config.OwnerUserId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                salesRepId: Config.OwnerUserId
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, "No Manager Standard commission should be generated when no prior sales rep commission exists.");
        }

        [TestMethod]
        public async Task ManagerStandardCommission_BusinessUnitMismatch_NoCommissionGenerated()
        {
            var caseDateTime = DateTime.UtcNow;
            var caseSetup = await SetupManagerStandardCaseAsync(caseDateTime);

            var team = await _teams.GetAsync(Seed.TeamId);
            team.TryGetProperty("_businessunitid_value", out var teamBusinessUnitId);
            Assert.IsNotNull(teamBusinessUnitId, "Team should have a business unit.");

            var systemUser = await _systemUsers.GetAsync(Config.OwnerUserId);
            systemUser.TryGetProperty("_businessunitid_value", out var ownerBusinessUnitId);
            Assert.IsNotNull(ownerBusinessUnitId, "Owner user should have a business unit.");
            Assert.AreNotEqual(teamBusinessUnitId.GetGuid(), ownerBusinessUnitId.GetGuid(), "Owner business unit should differ from team business unit.");

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await _salesRepContracts.SetupAsync(
                SalesRepContractConfig.Default(
                    contractType: OptionSetValues.SalesRepContract.ContractType.SalesRep,
                    statusCode: Config.DefaultStatusCode,
                    minCommissionRate: 5m,
                    maxCommissionRate: 10m,
                    startDate: contractStartDate,
                    ownerId: Seed.TeamId
                ) with
                {
                    Name = $"Manager Standard Team Contract {Config.CorrelationId}",
                    EndDate = contractEndDate,
                    SalesRepId = Config.OwnerUserId,
                    RepRole = OptionSetValues.SalesRepContract.RepRole.Manager,
                    IsOwnedByTeam = true,
                }
            );

            var standaloneRuleId = await CreateStandaloneCommissionRuleAsync(
                ownerId: Seed.TeamId,
                salesRepId: Config.OwnerUserId,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate,
                isOwnedByTeam: true
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Seed.TeamId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                businessUnitId: ownerBusinessUnitId.GetGuid(),
                isOwnedByTeam: true,
                salesRepId: Config.OwnerUserId
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "A prior Standalone commission should exist before Manager Standard commission is generated.");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, "No commission should be created when rule business unit does not match team-owned contract.");
        }

        [TestMethod]
        public async Task ManagerStandardCommission_Threshold2001Percent_CommissionGenerated()
        {
            var caseDateTime = DateTime.UtcNow;
            var costPrice = 159.98m;
            var caseSetup = await SetupManagerStandardCaseAsync(caseDateTime, costPrice: costPrice, unitPrice: 200.00m);

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await CreateManagerStandardContractAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                repRole: OptionSetValues.SalesRepContract.RepRole.Manager,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var standaloneRuleId = await CreateStandaloneCommissionRuleAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Config.OwnerUserId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                salesRepId: Config.OwnerUserId
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "A prior Standalone commission should exist before Manager Standard commission is generated.");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Commission should be generated when profit margin is 20.01%.");
        }

        [TestMethod]
        public async Task ManagerStandardCommission_Threshold1999Percent_NoCommissionGenerated()
        {
            var caseDateTime = DateTime.UtcNow;
            var costPrice = 160.02m;
            var caseSetup = await SetupManagerStandardCaseAsync(caseDateTime, costPrice: costPrice, unitPrice: 200.00m);

            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await CreateManagerStandardContractAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                repRole: OptionSetValues.SalesRepContract.RepRole.Manager,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var standaloneRuleId = await CreateStandaloneCommissionRuleAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Config.OwnerUserId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                salesRepId: Config.OwnerUserId
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "A prior Standalone commission should exist before Manager Standard commission is generated.");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, "No commission should be created when profit margin is 19.99%.");
        }

        [TestMethod]
        public async Task ManagerStandardCommission_CaseDateOnRuleStartDate_CommissionGenerated()
        {
            var contractStartDate = DateTime.UtcNow.Date;
            var caseSetup = await SetupManagerStandardCaseAsync(contractStartDate);

            var contractEndDate = contractStartDate.AddYears(1);
            var salesRepContractId = await CreateManagerStandardContractAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                repRole: OptionSetValues.SalesRepContract.RepRole.Manager,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var standaloneRuleId = await CreateStandaloneCommissionRuleAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Config.OwnerUserId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                salesRepId: Config.OwnerUserId
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "A prior Standalone commission should exist before Manager Standard commission is generated.");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Commission should be generated when case date is exactly the rule start date.");
        }

        [TestMethod]
        public async Task ManagerStandardCommission_CaseDateOnRuleEndDate_CommissionGenerated()
        {
            var contractStartDate = DateTime.UtcNow.Date;
            var contractEndDate = contractStartDate.AddDays(30);
            var caseSetup = await SetupManagerStandardCaseAsync(contractEndDate);

            var salesRepContractId = await CreateManagerStandardContractAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                repRole: OptionSetValues.SalesRepContract.RepRole.Manager,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var standaloneRuleId = await CreateStandaloneCommissionRuleAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Config.OwnerUserId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                salesRepId: Config.OwnerUserId
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "A prior Standalone commission should exist before Manager Standard commission is generated.");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(1, salesRepCommissions.Count, "Commission should be generated when case date is exactly the rule end date.");
        }

        [TestMethod]
        public async Task ManagerStandardCommission_CaseDateOutsideRuleRange_NoCommission()
        {
            var contractStartDate = DateTime.UtcNow.Date.AddDays(5);
            var caseSetup = await SetupManagerStandardCaseAsync(contractStartDate.AddDays(-1));

            var contractEndDate = contractStartDate.AddDays(30);
            var salesRepContractId = await CreateManagerStandardContractAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                repRole: OptionSetValues.SalesRepContract.RepRole.Manager,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var standaloneRuleId = await CreateStandaloneCommissionRuleAsync(
                ownerId: Config.OwnerUserId,
                salesRepId: Config.OwnerUserId,
                contractStartDate: contractStartDate,
                contractEndDate: contractEndDate
            );

            var managerRuleId = await CreateManagerStandardRuleAsync(
                ownerId: Config.OwnerUserId,
                contractId: salesRepContractId,
                ruleStartDate: contractStartDate,
                ruleEndDate: contractEndDate,
                salesRepId: Config.OwnerUserId
            );

            await _cases.RegenCaseAsync(caseSetup.CaseRecord);
            await _cases.WaitUntilRegenerateCompleteAsync(caseSetup.CaseRecord, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(5));

            var standaloneCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, standaloneRuleId);
            Assert.AreEqual(1, standaloneCommissions.Count, "A prior Standalone commission should exist before Manager Standard commission is evaluated.");

            var salesRepCommissions = await _salesRepCommissions.GetByCaseAndRuleAsync(caseSetup.CaseRecord, managerRuleId);
            Assert.AreEqual(0, salesRepCommissions.Count, "No commission should be created when case date is outside the rule effective range.");
        }

    }
}
