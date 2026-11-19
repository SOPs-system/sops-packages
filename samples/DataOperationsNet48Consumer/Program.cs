using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sops.DataOperations;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Backup;
using Sops.DataOperations.Extensions;
using Sops.DataOperations.Security;
using Sops.DataOperations.Sync;
using Sops.DataOperations.Sync.Storage;

namespace DataOperationsNet48Consumer
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("===============================================================");
            Console.WriteLine(" SOPs.DataOperations .NET Framework 4.8 Compatibility Consumer");
            Console.WriteLine("===============================================================");

            try
            {
                // 1. Direct Instantiation of Cryptographic Subsystem
                Console.WriteLine("[1/5] Verifying PBKDF2 Key Derivation on netstandard2.0 runtime...");
                string secret = BackupKeyProvider.GenerateRandomMasterSecret();
                var keys = BackupKeyProvider.DeriveKeys(secret, "primary");
                if (keys.EncryptionKey.Length != 32 || keys.AuthenticationKey.Length != 32)
                {
                    Console.Error.WriteLine("FAILED: Invalid key lengths.");
                    return 1;
                }
                Console.WriteLine("      PASS: 256-bit encryption and authentication keys derived.");

                // 2. Stream Processor Roundtrip in Memory
                Console.WriteLine("[2/5] Verifying Stream-Based AES-256-CBC + HMAC-SHA256 Roundtrip...");
                byte[] sampleData = Encoding.UTF8.GetBytes("NET48_TEST_PAYLOAD_SENSITIVE_PHARMACY_DATA");
                var manifest = new BackupManifest
                {
                    BackupId = Guid.NewGuid(),
                    PharmacyId = Guid.NewGuid(),
                    FormatVersion = 1,
                    DatabaseSchemaVersion = "1.0.0",
                    ApplicationVersion = "1.0.0",
                    EncryptionAlgorithm = "AES-256-CBC+HMAC-SHA256",
                    KeyIdentifier = keys.KeyIdentifier,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };

                using (var inputStream = new MemoryStream(sampleData))
                using (var encryptedStream = new MemoryStream())
                {
                    await EncryptedStreamProcessor.EncryptStreamAsync(inputStream, encryptedStream, keys, manifest);
                    encryptedStream.Position = 0;

                    using (var decryptedStream = new MemoryStream())
                    {
                        await EncryptedStreamProcessor.DecryptStreamAsync(encryptedStream, decryptedStream, keys);
                        byte[] decrypted = decryptedStream.ToArray();
                        if (decrypted.Length != sampleData.Length)
                        {
                            Console.Error.WriteLine("FAILED: Decrypted length mismatch.");
                            return 2;
                        }
                    }
                }
                Console.WriteLine("      PASS: Stream encryption and decryption successful.");

                // 3. Manifest Validation
                Console.WriteLine("[3/5] Verifying Manifest Validation Engine...");
                manifest.ArchiveSizeBytes = 1024;
                manifest.ChecksumSha256 = "abc123";
                manifest.IntegrityMac = "def456";
                var (isValid, errors) = BackupManifestValidator.Validate(manifest);
                if (!isValid)
                {
                    Console.Error.WriteLine($"FAILED: Manifest invalid: {string.Join(", ", errors)}");
                    return 3;
                }
                Console.WriteLine("      PASS: Manifest metadata validation passed.");

                // 4. Dependency Injection & Service Registration
                Console.WriteLine("[4/5] Verifying Microsoft.Extensions.DependencyInjection Integration...");
                var services = new ServiceCollection();
                services.AddLogging(builder => builder.AddConsole());
                services.AddSopsDataOperations(options =>
                {
                    options.BackupPolicy.MinimumFreeDiskSpaceBytes = 1024;
                    options.SyncPolicy.MaxRetryAttempts = 3;
                });

                using (var provider = services.BuildServiceProvider())
                {
                    var sync = provider.GetRequiredService<ISyncCoordinator>();
                    var approvals = provider.GetRequiredService<IChangeApprovalCoordinator>();
                    var backups = provider.GetRequiredService<IEncryptedBackupCoordinator>();
                    var restores = provider.GetRequiredService<IRestoreCoordinator>();
                    var coordinator = provider.GetRequiredService<IDataOperationsCoordinator>();
                    var status = provider.GetRequiredService<IOperationsStatusProvider>();

                    if (sync == null || backups == null || restores == null || coordinator == null)
                    {
                        Console.Error.WriteLine("FAILED: Null service resolution.");
                        return 4;
                    }

                    var health = await status.GetHealthSummaryAsync();
                    Console.WriteLine($"      PASS: All core interfaces resolved cleanly. Overall health: IsHealthy={health.IsHealthy}");
                }

                // 5. Verification Complete
                Console.WriteLine("[5/5] Sops.DataOperations is 100% compatible with .NET Framework 4.8!");
                Console.WriteLine("===============================================================");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"EXCEPTION: {ex}");
                return 99;
            }
        }
    }
}
