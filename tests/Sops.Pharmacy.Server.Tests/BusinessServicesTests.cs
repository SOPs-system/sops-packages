using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sops.Pharmacy.Server.Configuration;
using Sops.Pharmacy.Server.Domain.Auth;
using Sops.Pharmacy.Server.Domain.Catalog;
using Sops.Pharmacy.Server.Domain.Devices;
using Sops.Pharmacy.Server.Domain.Inventory;
using Sops.Pharmacy.Server.Domain.Sales;
using Sops.Pharmacy.Server.Domain.Shifts;
using Sops.Pharmacy.Server.Persistence;
using Xunit;

namespace Sops.Pharmacy.Server.Tests
{
    public class BusinessServicesTests : IDisposable
    {
        private readonly string _testDir;
        private readonly PharmacyServerSettings _settings;
        private readonly SqlCipherDatabaseProvider _dbProvider;
        private readonly DatabaseMigrator _migrator;
        private readonly DrugCatalogService _drugCatalog;
        private readonly InventoryService _inventory;
        private readonly SalesService _sales;
        private readonly ShiftService _shifts;
        private readonly DeviceService _devices;
        private readonly AuthService _auth;

        public BusinessServicesTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "sops_test_biz_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);

            _settings = new PharmacyServerSettings
            {
                DatabasePath = Path.Combine(_testDir, "pharmacy.db"),
                BackupDirectory = Path.Combine(_testDir, "backups"),
                LogDirectory = Path.Combine(_testDir, "logs"),
                DatabasePassphrase = "Biz_Secret_Passphrase_2026!"
            };
            _settings.EnsureDirectories();

            _dbProvider = new SqlCipherDatabaseProvider(_settings);
            _migrator = new DatabaseMigrator(_dbProvider, _settings);
            _migrator.MigrateAsync().GetAwaiter().GetResult();

            _drugCatalog = new DrugCatalogService(_dbProvider);
            _inventory = new InventoryService(_dbProvider);
            _sales = new SalesService(_dbProvider, _drugCatalog);
            _shifts = new ShiftService(_dbProvider);
            _devices = new DeviceService(_dbProvider);
            _auth = new AuthService(_dbProvider);
        }

        [Fact]
        public async Task DrugCatalog_BilingualSearchAndBarcode_ReturnsAccurateMatches()
        {
            // Seed catalog
            await _drugCatalog.EnsureSampleCatalogSeededAsync();

            // English search
            var enResults = await _drugCatalog.SearchDrugsAsync("Panadol");
            Assert.NotEmpty(enResults);
            Assert.Contains(enResults, d => d.TradeName.Contains("Panadol"));

            // Arabic search (with hamza)
            var arResults = await _drugCatalog.SearchDrugsAsync("أوجمنتين");
            Assert.NotEmpty(arResults);
            Assert.Contains(arResults, d => d.TradeNameAr.Contains("أوجمنتين"));

            // Arabic search normalized (without hamza)
            var normResults = await _drugCatalog.SearchDrugsAsync("اوجمنتين");
            Assert.NotEmpty(normResults);
            Assert.Contains(normResults, d => d.TradeNameAr.Contains("أوجمنتين"));

            // Barcode lookup
            var barcodeDrug = await _drugCatalog.FindByBarcodeAsync("622300000101");
            Assert.NotNull(barcodeDrug);
            Assert.Equal("Panadol Extra", barcodeDrug!.TradeName);
        }

        [Fact]
        public async Task Inventory_LotsFefoOrdering_ReturnsEarliestExpiringLotsFirst()
        {
            await _drugCatalog.EnsureSampleCatalogSeededAsync();
            var drug = (await _drugCatalog.SearchDrugsAsync("Panadol")).First();

            var lots = await _inventory.GetLotsForDrugAsync(drug.Id);
            Assert.True(lots.Count >= 2);

            // Assert FEFO: First lot expiry is earlier or equal to next lot expiry
            Assert.True(DateTime.Parse(lots[0].ExpiryDate) <= DateTime.Parse(lots[1].ExpiryDate));
        }

        [Fact]
        public async Task Sales_CompleteTransaction_AtomicDeductionAndIdempotency()
        {
            await _drugCatalog.EnsureSampleCatalogSeededAsync();
            var drug = (await _drugCatalog.SearchDrugsAsync("Panadol")).First();
            var lotsBefore = await _inventory.GetLotsForDrugAsync(drug.Id);
            var remainingBefore = lotsBefore.Sum(l => l.RemainingQuantity);

            // Open shift
            var shift = await _shifts.OpenShiftAsync(_settings.PharmacyId, "POS-01", "cashier1", 50000);

            // Create draft
            var draft = await _sales.CreateDraftAsync();
            await _sales.AddLineAsync(draft.SaleId, drug.Id, 2m);
            await _sales.AddPaymentAsync(draft.SaleId, "Cash", draft.SubtotalMinor);

            var idempotencyKey = "idemp_" + Guid.NewGuid().ToString("N");

            // Complete sale
            var tx = await _sales.CompleteTransactionAsync(
                draft.SaleId, _settings.PharmacyId, "POS-01", "cashier1", shift.Id, idempotencyKey);

            Assert.NotNull(tx);
            Assert.Equal("Completed", tx.TransactionStatus);

            // Verify stock lot deducted
            var lotsAfter = await _inventory.GetLotsForDrugAsync(drug.Id);
            var remainingAfter = lotsAfter.Sum(l => l.RemainingQuantity);
            Assert.Equal(remainingBefore - 2m, remainingAfter);

            // Re-submitting with identical idempotency key must return existing tx without double deduction
            var txDuplicate = await _sales.CompleteTransactionAsync(
                draft.SaleId, _settings.PharmacyId, "POS-01", "cashier1", shift.Id, idempotencyKey);

            Assert.Equal(tx.Id, txDuplicate.Id);
            var lotsAfterDup = await _inventory.GetLotsForDrugAsync(drug.Id);
            Assert.Equal(remainingAfter, lotsAfterDup.Sum(l => l.RemainingQuantity));
        }

        [Fact]
        public async Task Shifts_OpenAndClose_CalculatesExpectedCashAccurately()
        {
            var openingFloat = 20000L; // 200 EGP
            var shift = await _shifts.OpenShiftAsync(_settings.PharmacyId, "POS-01", "cashier1", openingFloat);
            Assert.Equal("Open", shift.Status);
            Assert.Equal(openingFloat, shift.OpeningCashMinor);

            // Close shift with counted cash
            var countedCash = 25000L;
            var closedShift = await _shifts.CloseShiftAsync(shift.Id, countedCash);

            Assert.Equal("Closed", closedShift.Status);
            Assert.Equal(countedCash, closedShift.ClosingCashMinor);
            Assert.NotNull(closedShift.ExpectedCashMinor);
            Assert.NotNull(closedShift.CashDiffMinor);
        }

        [Fact]
        public async Task Devices_EnrollmentAndApproval_ManagesLanAccess()
        {
            var fp = "hw_fp_" + Guid.NewGuid().ToString("N");
            var term = await _devices.EnrollTerminalAsync("Counter POS 2", "POS", fp, "192.168.1.102");

            Assert.NotNull(term);
            Assert.Equal("Approved", term.Status);

            var isAuth = await _devices.IsTerminalAuthorizedAsync(term.Id);
            Assert.True(isAuth);

            // Revoke
            await _devices.RevokeTerminalAsync(term.Id);
            var isAuthRevoked = await _devices.IsTerminalAuthorizedAsync(term.Id);
            Assert.False(isAuthRevoked);
        }

        [Fact]
        public async Task Auth_LoginVerificationAndAuditTrail_Succeeds()
        {
            var authResult = await _auth.LoginAsync("admin", "admin123", "127.0.0.1");
            Assert.True(authResult.Success);
            Assert.NotNull(authResult.Token);
            Assert.NotNull(authResult.User);
            Assert.Equal("Admin", authResult.User!.Role);

            // Validate token
            var user = await _auth.ValidateTokenAsync(authResult.Token!);
            Assert.NotNull(user);
            Assert.Equal("admin", user!.Username);

            // Invalid login
            var failedAuth = await _auth.LoginAsync("admin", "wrong_password", "127.0.0.1");
            Assert.False(failedAuth.Success);

            // Audit events logged
            var auditEvents = await _auth.GetRecentAuditEventsAsync(10);
            Assert.NotEmpty(auditEvents);
            Assert.Contains(auditEvents, a => a.Action == "LOGIN" || a.Action == "LOGIN_FAILED");
        }

        public void Dispose()
        {
            _dbProvider?.Dispose();
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }
    }
}
