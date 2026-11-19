using System;
using System.Security.Cryptography;
using System.Text;

namespace Sops.Drugs.Normalization
{
    /// <summary>
    /// Generates deterministic, stable unique identifiers and SKUs for drugs and categories.
    /// </summary>
    public static class IdentityGenerator
    {
        // Custom SOPs namespace UUID for deterministic v5-style UUID generation
        private static readonly Guid SopsDrugNamespace = new Guid("8a91340b-468b-4ad8-8c1e-355b8ef991a4");

        /// <summary>
        /// Generates a deterministic UUID based on a seed string.
        /// </summary>
        /// <param name="seed">The input string (e.g. normalized name + strength + form + pack).</param>
        /// <returns>A deterministic UUID string.</returns>
        public static string GenerateDeterministicId(string seed)
        {
            if (string.IsNullOrWhiteSpace(seed))
            {
                return Guid.NewGuid().ToString("D");
            }

            byte[] namespaceBytes = SopsDrugNamespace.ToByteArray();
            SwapGuidByteOrder(namespaceBytes);

            byte[] seedBytes = Encoding.UTF8.GetBytes(seed.Trim().ToLowerInvariant());
            byte[] combined = new byte[namespaceBytes.Length + seedBytes.Length];
            Buffer.BlockCopy(namespaceBytes, 0, combined, 0, namespaceBytes.Length);
            Buffer.BlockCopy(seedBytes, 0, combined, namespaceBytes.Length, seedBytes.Length);

            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(combined);

                // Set version 5 (name-based SHA-1)
                hash[6] = (byte)((hash[6] & 0x0F) | (5 << 4));
                // Set variant (RFC 4122)
                hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

                byte[] guidBytes = new byte[16];
                Buffer.BlockCopy(hash, 0, guidBytes, 0, 16);
                SwapGuidByteOrder(guidBytes);

                return new Guid(guidBytes).ToString("D");
            }
        }

        /// <summary>
        /// Generates a deterministic business SKU (e.g. "DRG-012345" or "DRG-ABCDEF12").
        /// </summary>
        /// <param name="sequentialIndex">A 1-based sequential number, or hash seed.</param>
        /// <returns>A formatted SKU string.</returns>
        public static string GenerateDrugSku(int sequentialIndex)
        {
            return $"DRG-{sequentialIndex:D6}";
        }

        /// <summary>
        /// Generates a deterministic category ID (e.g. "CAT-0001").
        /// </summary>
        /// <param name="categoryIndex">A 1-based sequential category index.</param>
        /// <returns>A formatted Category ID.</returns>
        public static string GenerateCategoryId(int categoryIndex)
        {
            return $"CAT-{categoryIndex:D4}";
        }

        private static void SwapGuidByteOrder(byte[] guid)
        {
            Swap(guid, 0, 3);
            Swap(guid, 1, 2);
            Swap(guid, 4, 5);
            Swap(guid, 6, 7);
        }

        private static void Swap(byte[] b, int i, int j)
        {
            byte temp = b[i];
            b[i] = b[j];
            b[j] = temp;
        }
    }
}
