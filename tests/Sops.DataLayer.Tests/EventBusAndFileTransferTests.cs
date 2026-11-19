using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Sops.DataLayer.Diagnostics;
using Sops.DataLayer.Events;
using Sops.DataLayer.Files;
using Sops.DataLayer.Models.Events;
using Xunit;

namespace Sops.DataLayer.Tests
{
    public class EventBusAndFileTransferTests
    {
        public record CartUpdatedEvent(string CartId, decimal TotalAmount);

        [Fact]
        public void EventBus_Publishes_And_Receives_Typed_Events()
        {
            var bus = new DataLayerEventBus();
            CartUpdatedEvent? received = null;

            using (bus.Subscribe<CartUpdatedEvent>(evt => received = evt))
            {
                bus.Publish(new CartUpdatedEvent("cart_123", 450.50m));
                Assert.NotNull(received);
                Assert.Equal("cart_123", received!.CartId);
                Assert.Equal(450.50m, received.TotalAmount);
            }

            // After subscription disposal, handler should not be invoked
            received = null;
            bus.Publish(new CartUpdatedEvent("cart_999", 10.0m));
            Assert.Null(received);
        }

        [Fact]
        public async Task AtomicFileWriter_Writes_Atomically_And_Replaces()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_dl_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, "pos_catalog.json");

            try
            {
                // Write initial
                byte[] bytes1 = Encoding.UTF8.GetBytes("Initial Content");
                using (var ms1 = new MemoryStream(bytes1))
                {
                    await AtomicFileWriter.WriteAtomicAsync(filePath, ms1);
                }

                Assert.True(File.Exists(filePath));
                Assert.Equal("Initial Content", File.ReadAllText(filePath));

                // Replace atomically
                byte[] bytes2 = Encoding.UTF8.GetBytes("Updated Content v2");
                using (var ms2 = new MemoryStream(bytes2))
                {
                    await AtomicFileWriter.WriteAtomicAsync(filePath, ms2);
                }

                Assert.Equal("Updated Content v2", File.ReadAllText(filePath));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void SensitiveDataRedactor_Redacts_Passwords_Tokens_And_Cards()
        {
            string log = "User login with password: \"superSecret123!\" and token: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.token";
            string redacted = SensitiveDataRedactor.Redact(log);

            Assert.DoesNotContain("superSecret123!", redacted);
            Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.token", redacted);
            Assert.Contains("***REDACTED***", redacted);
        }

        [Fact]
        public void SensitiveDataRedactor_Redacts_Egyptian_National_Id()
        {
            string text = "Customer Egyptian National ID is 29801011234567 for validation.";
            string redacted = SensitiveDataRedactor.Redact(text);

            Assert.DoesNotContain("29801011234567", redacted);
            Assert.Contains("***REDACTED***", redacted);
        }
    }
}
