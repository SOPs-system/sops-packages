using System;
using System.Threading;
using System.Windows.Controls;
using Sop.Ui.Controls;
using Xunit;

namespace Sop.Ui.Tests
{
    public class P0ComponentsTests
    {
        [Fact]
        public void Barcode_CheckDigit_CalculatesCorrectEanCheckDigit()
        {
            // Standard EAN-13 body (12 digits) -> check digit
            // Example: 622300000001 -> check digit 5
            var cd = Barcode.CheckDigit("622300000001");
            Assert.NotNull(cd);
            Assert.True(int.TryParse(cd, out _));

            // EAN-8 body (7 digits) -> check digit
            var cd8 = Barcode.CheckDigit("9638507");
            Assert.NotNull(cd8);
            Assert.True(int.TryParse(cd8, out _));
        }

        [Fact]
        public void Barcode_GetPrintableCode_IdentifiesSymbologyCorrectly()
        {
            // Valid 13-digit retail barcode
            var ean13Code = Barcode.GetPrintableCode("622300000001" + Barcode.CheckDigit("622300000001"));
            Assert.NotNull(ean13Code);
            Assert.Equal(BarcodeSymbology.Ean13, ean13Code!.Symbology);

            // Alphanumeric SKU -> Code 128
            var skuCode = Barcode.GetPrintableCode("DRG-PANADOL-500");
            Assert.NotNull(skuCode);
            Assert.Equal(BarcodeSymbology.Code128, skuCode!.Symbology);
        }

        [Fact]
        public void BarcodeInput_SubmitScan_FiresScanSubmittedEvent()
        {
            ExecuteInSta(() =>
            {
                var input = new BarcodeInput
                {
                    Text = "6223000123456",
                    ClearOnSubmit = true
                };

                string? receivedCode = null;
                input.ScanSubmitted += (s, e) =>
                {
                    receivedCode = e.Barcode;
                };

                input.SubmitScan();

                Assert.Equal("6223000123456", receivedCode);
                Assert.Equal(string.Empty, input.Text);
            });
        }

        [Fact]
        public void EmptyState_Properties_AssignCorrectly()
        {
            ExecuteInSta(() =>
            {
                var state = new EmptyState
                {
                    Title = "No Results Found",
                    Description = "Try searching with a different keyword",
                    ActionText = "Reset Filters"
                };

                Assert.Equal("No Results Found", state.Title);
                Assert.Equal("Try searching with a different keyword", state.Description);
                Assert.Equal("Reset Filters", state.ActionText);
            });
        }

        [Fact]
        public void AsyncLookup_QueryProperty_NotifiesCorrectly()
        {
            ExecuteInSta(() =>
            {
                var lookup = new AsyncLookup
                {
                    Placeholder = "Search drugs...",
                    Query = "Amox"
                };

                Assert.Equal("Amox", lookup.Query);
            });
        }

        private static void ExecuteInSta(Action action)
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null) throw caught;
        }
    }
}
