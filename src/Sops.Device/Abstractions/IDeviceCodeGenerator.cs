using Sops.Device.Models;

namespace Sops.Device.Abstractions
{
    /// <summary>
    /// Generates and validates human-readable, unambiguous, versioned device codes (e.g. SOP-WIN-7K2M-9Q4D).
    /// </summary>
    public interface IDeviceCodeGenerator
    {
        /// <summary>
        /// Generates a human-readable device code for the given installation ID and platform.
        /// </summary>
        /// <param name="installationId">Persistent installation UUID.</param>
        /// <param name="platform">Operating system platform.</param>
        /// <param name="prefix">Optional custom prefix (defaults to configured prefix).</param>
        /// <returns>Formatted device code.</returns>
        string GenerateCode(string installationId, DevicePlatform platform, string? prefix = null);

        /// <summary>
        /// Validates the format, character set, and checksum of a device code string.
        /// </summary>
        /// <param name="code">Device code string to validate.</param>
        /// <param name="error">Output description if validation failed.</param>
        /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
        bool ValidateCode(string code, out string? error);

        /// <summary>
        /// Validates the format, character set, and checksum of a device code string and outputs its version.
        /// </summary>
        /// <param name="code">Device code string to validate.</param>
        /// <param name="version">Detected device code format version (1 for V1 standard Crockford Base32).</param>
        /// <param name="error">Output description if validation failed.</param>
        /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
        bool ValidateCode(string code, out int version, out string? error);
    }
}
