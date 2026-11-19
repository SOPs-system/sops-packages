using Sops.Licensing.Models;

namespace Sops.Licensing.Abstractions
{
    /// <summary>
    /// Computes stable, privacy-preserving installation and hardware signals for node-locked license binding.
    /// Supports fuzzy hardware matching tolerance so legitimate single-component upgrades do not break activated licenses.
    /// </summary>
    public interface IDeviceIdentifier
    {
        /// <summary>
        /// Generates the current machine's <see cref="DeviceFingerprint"/>.
        /// </summary>
        /// <returns>A structured fingerprint including individual signal hashes and combined hash.</returns>
        DeviceFingerprint GetCurrentFingerprint();

        /// <summary>
        /// Compares the current machine's hardware signals against a stored license binding hash.
        /// </summary>
        /// <param name="storedBindingHash">The expected binding hash recorded in the license.</param>
        /// <param name="tolerance">Matching tolerance (0.0 to 1.0, e.g. 0.7 requires 70% of signals to match).</param>
        /// <returns><c>true</c> if the device matches within acceptable tolerance; otherwise, <c>false</c>.</returns>
        bool Matches(string storedBindingHash, double tolerance = 0.7);
    }
}
