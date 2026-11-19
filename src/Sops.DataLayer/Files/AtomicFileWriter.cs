using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.DataLayer.Files
{
    /// <summary>
    /// Safely writes files atomically via temporary staging files to avoid corruption during crashes or power losses.
    /// </summary>
    public static class AtomicFileWriter
    {
        public static async Task WriteAtomicAsync(string destinationPath, Stream contentStream, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("Destination path cannot be empty", nameof(destinationPath));
            if (contentStream == null) throw new ArgumentNullException(nameof(contentStream));

            string? directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = Path.Combine(directory ?? ".", $"{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await contentStream.CopyToAsync(fileStream, 81920, cancellationToken).ConfigureAwait(false);
                    await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(tempPath, destinationPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }
    }
}
