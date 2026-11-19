using System;
using System.Runtime.CompilerServices;

namespace Sops.Licensing.Cryptography
{
    /// <summary>
    /// Constant-time comparison utilities to eliminate side-channel timing attack vulnerabilities.
    /// </summary>
    internal static class ConstantTime
    {
        /// <summary>
        /// Compares two byte arrays in constant time.
        /// </summary>
        /// <param name="a">First byte sequence.</param>
        /// <param name="b">Second byte sequence.</param>
        /// <returns><c>true</c> if both byte arrays have identical length and contents; otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static bool Equals(byte[]? a, byte[]? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }

        /// <summary>
        /// Compares two strings in constant time.
        /// </summary>
        public static bool Equals(string? a, string? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            byte[] bytesA = System.Text.Encoding.UTF8.GetBytes(a);
            byte[] bytesB = System.Text.Encoding.UTF8.GetBytes(b);
            return Equals(bytesA, bytesB);
        }

        /// <summary>
        /// Securely overwrites a byte array with zeros in memory.
        /// </summary>
        /// <param name="buffer">The buffer to wipe.</param>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void ZeroMemory(byte[]? buffer)
        {
            if (buffer == null) return;
            Array.Clear(buffer, 0, buffer.Length);
        }
    }
}
