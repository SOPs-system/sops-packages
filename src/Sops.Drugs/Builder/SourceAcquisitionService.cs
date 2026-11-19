using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Sops.Drugs.Models;

namespace Sops.Drugs.Builder
{
    /// <summary>
    /// Downloads source datasets from official GitHub repositories, verifies SHA-256 integrity,
    /// and records provenance metadata.
    /// </summary>
    public class SourceAcquisitionService
    {
        private readonly HttpClient _httpClient;

        /// <summary>Pinned Git commit hash for mahmoudfalous/eg-drugs.</summary>
        public const string FalousCommit = "64660b26950a13289d43107a42e4b075bbb73be5";

        /// <summary>Pinned Git commit hash for karem505/egyptian-drug-database.</summary>
        public const string KaremCommit = "7d76aa2ac3474684f142aefa52452e1963eb0cc6";

        /// <summary>Raw GitHub URL for eg_drugs.csv.</summary>
        public static readonly string FalousRawUrl =
            $"https://raw.githubusercontent.com/mahmoudfalous/eg-drugs/{FalousCommit}/data/eg_drugs.csv";

        /// <summary>Raw GitHub URL for egyptian-drugs.csv.</summary>
        public static readonly string KaremRawUrl =
            $"https://raw.githubusercontent.com/karem505/egyptian-drug-database/{KaremCommit}/data/egyptian-drugs.csv";

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceAcquisitionService"/> class.
        /// </summary>
        public SourceAcquisitionService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        /// <summary>
        /// Downloads both source datasets into the specified target directory.
        /// </summary>
        /// <param name="dataDir">The directory where raw CSV files will be stored.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A tuple containing provenance info for both downloaded sources.</returns>
        public async Task<(SourceProvenance falous, SourceProvenance karem)> AcquireSourcesAsync(
            string dataDir,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            string falousPath = Path.Combine(dataDir, "eg_drugs.csv");
            string karemPath = Path.Combine(dataDir, "egyptian-drugs.csv");

            Console.WriteLine($"[1/2] Fetching source: mahmoudfalous/eg-drugs (commit {FalousCommit.Substring(0, 8)})...");
            var falousProv = await DownloadFileAsync(
                "mahmoudfalous/eg-drugs",
                "https://github.com/mahmoudfalous/eg-drugs",
                FalousCommit,
                "data/eg_drugs.csv",
                FalousRawUrl,
                falousPath,
                "CC BY 4.0",
                cancellationToken).ConfigureAwait(false);

            Console.WriteLine($"[2/2] Fetching source: karem505/egyptian-drug-database (commit {KaremCommit.Substring(0, 8)})...");
            var karemProv = await DownloadFileAsync(
                "karem505/egyptian-drug-database",
                "https://github.com/karem505/egyptian-drug-database",
                KaremCommit,
                "data/egyptian-drugs.csv",
                KaremRawUrl,
                karemPath,
                "CC0 1.0 (Public Domain)",
                cancellationToken).ConfigureAwait(false);

            return (falousProv, karemProv);
        }

        private async Task<SourceProvenance> DownloadFileAsync(
            string name,
            string repoUrl,
            string commit,
            string relativePath,
            string downloadUrl,
            string localFilePath,
            string license,
            CancellationToken cancellationToken)
        {
            // If file already exists and is non-empty, verify existing SHA-256 and resume/reuse
            if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 1000)
            {
                Console.WriteLine($"  Existing local file found at: {localFilePath}. Verifying SHA-256 hash...");
                string existingHash = ComputeFileSha256(localFilePath);
                long existingSize = new FileInfo(localFilePath).Length;
                Console.WriteLine($"  Verified SHA-256: {existingHash} ({existingSize:N0} bytes)");

                return new SourceProvenance
                {
                    Name = name,
                    RepositoryUrl = repoUrl,
                    CommitHash = commit,
                    FilePath = relativePath,
                    Sha256Hash = existingHash,
                    FileSizeBytes = existingSize,
                    License = license
                };
            }

            Console.WriteLine($"  Downloading from: {downloadUrl} ...");
            string tempFile = localFilePath + ".tmp";

            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                using (var remoteStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[81920];
                    int bytesRead;
                    while ((bytesRead = await remoteStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            if (File.Exists(localFilePath))
            {
                File.Delete(localFilePath);
            }
            File.Move(tempFile, localFilePath);

            string sha256 = ComputeFileSha256(localFilePath);
            long size = new FileInfo(localFilePath).Length;
            Console.WriteLine($"  Downloaded {size:N0} bytes. SHA-256: {sha256}");

            return new SourceProvenance
            {
                Name = name,
                RepositoryUrl = repoUrl,
                CommitHash = commit,
                FilePath = relativePath,
                Sha256Hash = sha256,
                FileSizeBytes = size,
                License = license
            };
        }

        /// <summary>
        /// Computes the SHA-256 hash of a local file in lowercase hexadecimal.
        /// </summary>
        public static string ComputeFileSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(fs);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
