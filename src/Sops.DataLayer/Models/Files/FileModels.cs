using System;
using System.IO;

namespace Sops.DataLayer.Models.Files
{
    /// <summary>
    /// Progress update during upload or download transfer.
    /// </summary>
    public sealed class TransferProgress
    {
        public long BytesTransferred { get; }
        public long? TotalBytes { get; }
        public double? Percentage => TotalBytes.HasValue && TotalBytes.Value > 0 ? (double)BytesTransferred / TotalBytes.Value * 100.0 : null;
        public TimeSpan Elapsed { get; }

        public TransferProgress(long bytesTransferred, long? totalBytes, TimeSpan elapsed)
        {
            BytesTransferred = Math.Max(0, bytesTransferred);
            TotalBytes = totalBytes;
            Elapsed = elapsed;
        }
    }

    /// <summary>
    /// File upload specification with progress reporting and checksum verification.
    /// </summary>
    public sealed class UploadRequest
    {
        public Stream ContentStream { get; }
        public string FileName { get; }
        public string ContentType { get; }
        public string EndpointId { get; }
        public string? ExpectedSha256 { get; }
        public IProgress<TransferProgress>? Progress { get; }

        public UploadRequest(
            Stream contentStream,
            string fileName,
            string endpointId = "files.upload",
            string contentType = "application/octet-stream",
            string? expectedSha256 = null,
            IProgress<TransferProgress>? progress = null)
        {
            ContentStream = contentStream ?? throw new ArgumentNullException(nameof(contentStream));
            FileName = !string.IsNullOrWhiteSpace(fileName) ? fileName : "file.bin";
            EndpointId = endpointId;
            ContentType = contentType;
            ExpectedSha256 = expectedSha256;
            Progress = progress;
        }
    }

    /// <summary>
    /// File download specification writing atomically to a temporary destination.
    /// </summary>
    public sealed class DownloadRequest
    {
        public string DestinationFilePath { get; }
        public string EndpointId { get; }
        public string? ExpectedSha256 { get; }
        public IProgress<TransferProgress>? Progress { get; }

        public DownloadRequest(
            string destinationFilePath,
            string endpointId = "files.download",
            string? expectedSha256 = null,
            IProgress<TransferProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(destinationFilePath)) throw new ArgumentException("Destination path cannot be empty.", nameof(destinationFilePath));

            DestinationFilePath = destinationFilePath;
            EndpointId = endpointId;
            ExpectedSha256 = expectedSha256;
            Progress = progress;
        }
    }
}
