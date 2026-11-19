using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions.Models;

namespace Sops.DataOperations.Security
{
    /// <summary>
    /// Stream-based, bounded-memory authenticated encrypt-then-MAC processor (AES-256-CBC + HMAC-SHA256).
    /// Prevents entire database byte arrays in RAM and guarantees zero plaintext disk snapshots.
    /// </summary>
    public static class EncryptedStreamProcessor
    {
        private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("SOPSBK01");
        private const int ChunkSize = 65536; // 64 KB streaming buffer

        /// <summary>
        /// Reads exact count bytes from stream, preventing partial read defects.
        /// </summary>
        public static async Task ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken ct = default)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, ct);
                if (read == 0)
                    throw new EndOfStreamException($"Unexpected end of stream while reading {count} bytes (got {totalRead}).");
                totalRead += read;
            }
        }

        /// <summary>
        /// Encrypts an input stream to an output archive with authenticated manifest and HMAC trailer.
        /// </summary>
        public static async Task<BackupManifest> EncryptStreamAsync(
            Stream sourceStream,
            Stream outputStream,
            BackupKeyMaterial keys,
            BackupManifest manifest,
            IProgress<OperationProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            if (sourceStream == null) throw new ArgumentNullException(nameof(sourceStream));
            if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            // Precompute source SHA256 if stream is seekable
            if (sourceStream.CanSeek && string.IsNullOrWhiteSpace(manifest.ChecksumSha256))
            {
                using (var sha = SHA256.Create())
                {
                    long initialPos = sourceStream.Position;
                    byte[] hash = sha.ComputeHash(sourceStream);
                    manifest.ChecksumSha256 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    sourceStream.Position = initialPos;
                }
            }

            // Generate 16-byte random IV
            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            // Serialize Manifest JSON (provisional without final hashes)
            manifest.KeyIdentifier = keys.KeyIdentifier;
            byte[] keyIdBytes = Encoding.UTF8.GetBytes(keys.KeyIdentifier);
            byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));

            // Setup HMAC-SHA256 stream
            using (var hmac = new HMACSHA256(keys.AuthenticationKey))
            using (var sourceSha = SHA256.Create())
            {
                // Write Magic
                await outputStream.WriteAsync(MagicBytes, 0, MagicBytes.Length);
                hmac.TransformBlock(MagicBytes, 0, MagicBytes.Length, null, 0);

                // Write Key Identifier
                byte[] keyIdLen = BitConverter.GetBytes((short)keyIdBytes.Length);
                await outputStream.WriteAsync(keyIdLen, 0, keyIdLen.Length);
                hmac.TransformBlock(keyIdLen, 0, keyIdLen.Length, null, 0);

                await outputStream.WriteAsync(keyIdBytes, 0, keyIdBytes.Length);
                hmac.TransformBlock(keyIdBytes, 0, keyIdBytes.Length, null, 0);

                // Write Manifest
                byte[] manifestLen = BitConverter.GetBytes(manifestBytes.Length);
                await outputStream.WriteAsync(manifestLen, 0, manifestLen.Length);
                hmac.TransformBlock(manifestLen, 0, manifestLen.Length, null, 0);

                await outputStream.WriteAsync(manifestBytes, 0, manifestBytes.Length);
                hmac.TransformBlock(manifestBytes, 0, manifestBytes.Length, null, 0);

                // Write IV
                await outputStream.WriteAsync(iv, 0, iv.Length);
                hmac.TransformBlock(iv, 0, iv.Length, null, 0);

                // Setup AES-256-CBC
                using (var aes = Aes.Create())
                {
                    aes.Key = keys.EncryptionKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    long totalSourceBytes = sourceStream.CanSeek ? sourceStream.Length : 0;
                    long bytesProcessed = 0;

                    using (var encryptor = aes.CreateEncryptor())
                    using (var memorySink = new CryptoSinkStream(outputStream, hmac))
                    using (var cryptoStream = new CryptoStream(memorySink, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] buffer = new byte[ChunkSize];
                        int read;
                        while ((read = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();

                            sourceSha.TransformBlock(buffer, 0, read, null, 0);
                            await cryptoStream.WriteAsync(buffer, 0, read);

                            bytesProcessed += read;
                            if (totalSourceBytes > 0 && progress != null)
                            {
                                progress.Report(new OperationProgressReport
                                {
                                    OperationName = "DatabaseBackup",
                                    CurrentStep = "EncryptingStream",
                                    BytesProcessed = bytesProcessed,
                                    TotalBytes = totalSourceBytes,
                                    Percentage = Math.Round((double)bytesProcessed / totalSourceBytes * 100.0, 1)
                                });
                            }
                        }

                        cryptoStream.FlushFinalBlock();
                    }
                }

                // Finalize source SHA256 checksum
                sourceSha.TransformFinalBlock(new byte[0], 0, 0);
                manifest.ChecksumSha256 = sourceSha.Hash != null ? BitConverter.ToString(sourceSha.Hash).Replace("-", "").ToLowerInvariant() : string.Empty;

                // Finalize HMAC
                hmac.TransformFinalBlock(new byte[0], 0, 0);
                byte[] finalMac = hmac.Hash ?? new byte[32];
                manifest.IntegrityMac = BitConverter.ToString(finalMac).Replace("-", "").ToLowerInvariant();

                // Append HMAC tag to the end of archive (32 bytes)
                await outputStream.WriteAsync(finalMac, 0, finalMac.Length);
            }

            manifest.ArchiveSizeBytes = outputStream.Length;
            return manifest;
        }

        /// <summary>
        /// Reads and validates the un-decrypted archive header and manifest.
        /// </summary>
        public static async Task<ArchiveHeaderInfo> ReadHeaderAsync(Stream archiveStream, CancellationToken ct = default)
        {
            if (archiveStream == null) throw new ArgumentNullException(nameof(archiveStream));
            if (!archiveStream.CanSeek) throw new ArgumentException("Archive stream must be seekable.", nameof(archiveStream));

            archiveStream.Seek(0, SeekOrigin.Begin);

            // Read Magic
            byte[] magic = new byte[MagicBytes.Length];
            await ReadExactAsync(archiveStream, magic, 0, magic.Length, ct);
            if (!FixedTimeEquals(magic, MagicBytes))
                throw new InvalidDataException("Invalid backup archive format: Magic signature mismatch.");

            // Read Key Identifier
            byte[] keyIdLenBytes = new byte[2];
            await ReadExactAsync(archiveStream, keyIdLenBytes, 0, 2, ct);
            short keyIdLen = BitConverter.ToInt16(keyIdLenBytes, 0);
            if (keyIdLen < 0 || keyIdLen > 1024)
                throw new InvalidDataException("Corrupt archive header: Key identifier length out of bounds.");

            byte[] keyIdBytes = new byte[keyIdLen];
            await ReadExactAsync(archiveStream, keyIdBytes, 0, keyIdLen, ct);
            string keyId = Encoding.UTF8.GetString(keyIdBytes);

            // Read Manifest
            byte[] manifestLenBytes = new byte[4];
            await ReadExactAsync(archiveStream, manifestLenBytes, 0, 4, ct);
            int manifestLen = BitConverter.ToInt32(manifestLenBytes, 0);
            if (manifestLen <= 0 || manifestLen > 10 * 1024 * 1024) // 10 MB sanity limit
                throw new InvalidDataException("Corrupt archive header: Manifest length out of bounds.");

            byte[] manifestBytes = new byte[manifestLen];
            await ReadExactAsync(archiveStream, manifestBytes, 0, manifestLen, ct);
            string manifestJson = Encoding.UTF8.GetString(manifestBytes);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson);
            if (manifest == null)
                throw new InvalidDataException("Corrupt archive header: Manifest could not be parsed.");

            // Read IV
            byte[] iv = new byte[16];
            await ReadExactAsync(archiveStream, iv, 0, 16, ct);

            long cipherTextStart = archiveStream.Position;
            long cipherTextLength = archiveStream.Length - cipherTextStart - 32; // Exclude 32-byte HMAC trailer

            if (cipherTextLength < 0)
                throw new InvalidDataException("Archive is truncated: file is smaller than header and HMAC trailer.");

            // Read trailer HMAC (last 32 bytes)
            long currentPos = archiveStream.Position;
            archiveStream.Seek(archiveStream.Length - 32, SeekOrigin.Begin);
            byte[] trailerHmac = new byte[32];
            await ReadExactAsync(archiveStream, trailerHmac, 0, 32, ct);
            archiveStream.Seek(currentPos, SeekOrigin.Begin);

            manifest.ArchiveSizeBytes = archiveStream.Length;
            manifest.IntegrityMac = BitConverter.ToString(trailerHmac).Replace("-", "").ToLowerInvariant();

            return new ArchiveHeaderInfo
            {
                KeyIdentifier = keyId,
                Manifest = manifest,
                IV = iv,
                CiphertextOffset = cipherTextStart,
                CiphertextLength = cipherTextLength
            };
        }

        /// <summary>
        /// Authenticates the archive by verifying the HMAC-SHA256 signature over the entire payload before trusting decryption.
        /// </summary>
        public static async Task<bool> AuthenticateArchiveAsync(Stream archiveStream, byte[] authKey, CancellationToken ct = default)
        {
            if (archiveStream == null) throw new ArgumentNullException(nameof(archiveStream));
            if (authKey == null || authKey.Length != 32) throw new ArgumentException("Authentication key must be 32 bytes.", nameof(authKey));
            if (!archiveStream.CanSeek) throw new ArgumentException("Archive stream must be seekable.", nameof(archiveStream));

            long payloadLength = archiveStream.Length - 32;
            if (payloadLength <= 0) return false;

            archiveStream.Seek(0, SeekOrigin.Begin);

            using (var hmac = new HMACSHA256(authKey))
            {
                byte[] buffer = new byte[ChunkSize];
                long remaining = payloadLength;

                while (remaining > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    int toRead = (int)Math.Min(buffer.Length, remaining);
                    int read = await archiveStream.ReadAsync(buffer, 0, toRead);
                    if (read <= 0) return false;

                    hmac.TransformBlock(buffer, 0, read, null, 0);
                    remaining -= read;
                }

                hmac.TransformFinalBlock(new byte[0], 0, 0);
                byte[] calculatedMac = hmac.Hash ?? new byte[32];

                // Read expected MAC from trailer
                byte[] expectedMac = new byte[32];
                await ReadExactAsync(archiveStream, expectedMac, 0, 32, ct);

                return FixedTimeEquals(calculatedMac, expectedMac);
            }
        }

        /// <summary>
        /// Authenticates and decrypts the archive stream into target stream using bounded memory chunks.
        /// </summary>
        public static async Task DecryptStreamAsync(
            Stream archiveStream,
            Stream targetStream,
            BackupKeyMaterial keys,
            IProgress<OperationProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            if (archiveStream == null) throw new ArgumentNullException(nameof(archiveStream));
            if (targetStream == null) throw new ArgumentNullException(nameof(targetStream));
            if (keys == null) throw new ArgumentNullException(nameof(keys));

            // 1. Authenticate before trusting decrypted content
            bool isMacValid = await AuthenticateArchiveAsync(archiveStream, keys.AuthenticationKey, ct);
            if (!isMacValid)
                throw new CryptographicException("Archive authentication failed! The backup has been tampered with, corrupted, or the wrong key was provided.");

            // 2. Read Header
            var header = await ReadHeaderAsync(archiveStream, ct);

            // 3. Decrypt Ciphertext
            archiveStream.Seek(header.CiphertextOffset, SeekOrigin.Begin);

            using (var aes = Aes.Create())
            {
                aes.Key = keys.EncryptionKey;
                aes.IV = header.IV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var subStream = new BoundedSubStream(archiveStream, header.CiphertextLength))
                using (var cryptoStream = new CryptoStream(subStream, decryptor, CryptoStreamMode.Read))
                {
                    byte[] buffer = new byte[ChunkSize];
                    int read;
                    long bytesProcessed = 0;

                    while ((read = await cryptoStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        await targetStream.WriteAsync(buffer, 0, read);

                        bytesProcessed += read;
                        if (header.CiphertextLength > 0 && progress != null)
                        {
                            progress.Report(new OperationProgressReport
                            {
                                OperationName = "DatabaseRestore",
                                CurrentStep = "DecryptingStream",
                                BytesProcessed = bytesProcessed,
                                TotalBytes = header.CiphertextLength,
                                Percentage = Math.Min(100.0, Math.Round((double)bytesProcessed / header.CiphertextLength * 100.0, 1))
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Constant-time byte array comparison preventing timing side-channel attacks.
        /// </summary>
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        private sealed class CryptoSinkStream : Stream
        {
            private readonly Stream _inner;
            private readonly HMACSHA256 _hmac;

            public CryptoSinkStream(Stream inner, HMACSHA256 hmac)
            {
                _inner = inner;
                _hmac = hmac;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                _inner.Write(buffer, offset, count);
                _hmac.TransformBlock(buffer, offset, count, null, 0);
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await _inner.WriteAsync(buffer, offset, count, cancellationToken);
                _hmac.TransformBlock(buffer, offset, count, null, 0);
            }
        }

        private sealed class BoundedSubStream : Stream
        {
            private readonly Stream _inner;
            private readonly long _maxBytes;
            private long _bytesRead;

            public BoundedSubStream(Stream inner, long maxBytes)
            {
                _inner = inner;
                _maxBytes = maxBytes;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _maxBytes;
            public override long Position { get => _bytesRead; set => throw new NotSupportedException(); }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                long remaining = _maxBytes - _bytesRead;
                if (remaining <= 0) return 0;

                int toRead = (int)Math.Min(count, remaining);
                int read = _inner.Read(buffer, offset, toRead);
                _bytesRead += read;
                return read;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                long remaining = _maxBytes - _bytesRead;
                if (remaining <= 0) return 0;

                int toRead = (int)Math.Min(count, remaining);
                int read = await _inner.ReadAsync(buffer, offset, toRead, cancellationToken);
                _bytesRead += read;
                return read;
            }
        }
    }

    /// <summary>
    /// Parsed metadata from backup archive header.
    /// </summary>
    public sealed class ArchiveHeaderInfo
    {
        public string KeyIdentifier { get; set; } = string.Empty;
        public BackupManifest Manifest { get; set; } = new BackupManifest();
        public byte[] IV { get; set; } = new byte[16];
        public long CiphertextOffset { get; set; }
        public long CiphertextLength { get; set; }
    }
}
