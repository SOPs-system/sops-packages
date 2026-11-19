using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sops.Device.Models;

namespace Sops.Device.Fingerprinting
{
    /// <summary>
    /// Compares two hardware fingerprints and evaluates hardware drift, component replacement,
    /// and confidence levels without penalizing routine maintenance or network topology changes.
    /// </summary>
    public static class DeviceChangeDetector
    {
        private static double GetSignalWeight(string signalName, ref double accumulatedNetworkWeight, double maxNetworkCap = 2.0)
        {
            if (string.IsNullOrEmpty(signalName)) return 1.0;

            if (signalName.IndexOf("uuid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                signalName.IndexOf("guid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                signalName.IndexOf("motherboard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                signalName.IndexOf("baseboard", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 4.0; // Core hardware anchor
            }

            if (signalName.IndexOf("drive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                signalName.IndexOf("disk", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 3.0; // Primary storage anchor
            }

            if (signalName.IndexOf("bios", StringComparison.OrdinalIgnoreCase) >= 0 ||
                signalName.IndexOf("machineid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                signalName.IndexOf("processor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                signalName.IndexOf("vendor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2.0; // Major system component
            }

            if (signalName.IndexOf("mac", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Network adapters: primary gets 1.2, secondary get 0.5, capped at maxNetworkCap
                double remainingCap = Math.Max(0.0, maxNetworkCap - accumulatedNetworkWeight);
                if (remainingCap <= 0.0) return 0.2; // minimal nominal contribution

                double allocated = (accumulatedNetworkWeight == 0) ? Math.Min(1.2, remainingCap) : Math.Min(0.5, remainingCap);
                accumulatedNetworkWeight += allocated;
                return allocated;
            }

            return 1.0; // General / secondary signal
        }

        /// <summary>
        /// Compares a previous device fingerprint against the current fingerprint.
        /// </summary>
        /// <param name="previous">Previously captured fingerprint.</param>
        /// <param name="current">Current device fingerprint.</param>
        /// <param name="maxNetworkCap">Maximum combined weight cap for all network adapters.</param>
        /// <param name="evaluatedSignals">Optional live signals to distinguish access-denied/unavailable from physical removal.</param>
        /// <returns>Structured comparison result.</returns>
        public static DeviceIdentityComparison Compare(
            DeviceFingerprint? previous,
            DeviceFingerprint? current,
            double maxNetworkCap = 2.0,
            IReadOnlyList<DeviceSignal>? evaluatedSignals = null)
        {
            if (previous == null || string.IsNullOrEmpty(previous.CombinedHash))
            {
                return new DeviceIdentityComparison(
                    hasChanged: false,
                    similarityScore: 1.0,
                    confidence: DeviceComparisonConfidence.ExactMatch,
                    matchedSignals: current?.IncludedSignals ?? Array.Empty<string>(),
                    changedSignals: Array.Empty<string>(),
                    addedSignals: current?.IncludedSignals ?? Array.Empty<string>(),
                    removedSignals: Array.Empty<string>(),
                    assessment: "Initial baseline fingerprint established; no previous fingerprint for comparison.");
            }

            if (current == null || string.IsNullOrEmpty(current.CombinedHash))
            {
                return new DeviceIdentityComparison(
                    hasChanged: true,
                    similarityScore: 0.0,
                    confidence: DeviceComparisonConfidence.DifferentDevice,
                    matchedSignals: Array.Empty<string>(),
                    changedSignals: Array.Empty<string>(),
                    addedSignals: Array.Empty<string>(),
                    removedSignals: previous.IncludedSignals,
                    assessment: "Current device fingerprint is missing or empty; hardware signals could not be gathered.");
            }

            // Quick path: exact combined hash match and matching scope
            if (string.Equals(previous.CombinedHash, current.CombinedHash, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(previous.Scope, current.Scope, StringComparison.OrdinalIgnoreCase) &&
                previous.FingerprintVersion == current.FingerprintVersion)
            {
                return DeviceIdentityComparison.ExactMatch(current.IncludedSignals);
            }

            // Check for algorithm, scope, or version mismatch when raw signals are unavailable
            bool incompatibleCrypto = previous.FingerprintVersion != current.FingerprintVersion ||
                                      previous.Algorithm != current.Algorithm ||
                                      previous.IsKeyed != current.IsKeyed ||
                                      !string.Equals(previous.Scope, current.Scope, StringComparison.OrdinalIgnoreCase);

            if (incompatibleCrypto &&
                (evaluatedSignals == null || !evaluatedSignals.Any(s => !string.IsNullOrEmpty(s.NormalizedValue))))
            {
                return DeviceIdentityComparison.IncomparableMigration(
                    previous.FingerprintVersion,
                    current.FingerprintVersion,
                    $"Fingerprints use differing cryptographic configurations (v{previous.FingerprintVersion} [{previous.Algorithm}, Scope: '{previous.Scope}'] vs v{current.FingerprintVersion} [{current.Algorithm}, Scope: '{current.Scope}']) and cannot be mathematically compared without live normalized signals.");
            }

            // Identify any signals that were denied permission or temporarily unavailable in live evaluation
            var deniedOrUnavailable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (evaluatedSignals != null)
            {
                foreach (var s in evaluatedSignals)
                {
                    if (s.Status == SignalStatus.AccessDenied || s.Status == SignalStatus.Unavailable || s.Status == SignalStatus.Failed)
                    {
                        deniedOrUnavailable.Add(s.Name);
                    }
                }
            }

            var matched = new List<string>();
            var changed = new List<string>();
            var added = new List<string>();
            var removed = new List<string>();
            var inaccessible = new List<string>();

            double matchedWeight = 0;
            double totalWeight = 0;
            double accumulatedNetworkWeight = 0;

            var allKeys = new HashSet<string>(previous.SignalHashes.Keys, StringComparer.OrdinalIgnoreCase);
            allKeys.UnionWith(current.SignalHashes.Keys);

            // Sort keys canonically so network weights allocate deterministically
            var sortedKeys = allKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var key in sortedKeys)
            {
                double weight = GetSignalWeight(key, ref accumulatedNetworkWeight, maxNetworkCap);

                bool inPrev = previous.SignalHashes.TryGetValue(key, out var prevHash);
                bool inCurr = current.SignalHashes.TryGetValue(key, out var currHash);

                if (inPrev && inCurr)
                {
                    totalWeight += weight;
                    bool isMatch = string.Equals(prevHash, currHash, StringComparison.OrdinalIgnoreCase);

                    // If hashes differ due to algorithm/version migration, test raw signal continuity if evaluated signals are available
                    if (!isMatch && previous.FingerprintVersion != current.FingerprintVersion && evaluatedSignals != null)
                    {
                        var matchingSig = evaluatedSignals.FirstOrDefault(s => string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase));
                        if (matchingSig?.NormalizedValue != null && !previous.IsKeyed)
                        {
                            string candidateV1Hash = DeviceSignalNormalizer.ComputeSha256(matchingSig.NormalizedValue, previous.Scope);
                            if (string.Equals(candidateV1Hash, prevHash, StringComparison.OrdinalIgnoreCase))
                            {
                                isMatch = true;
                            }
                        }
                    }

                    if (isMatch)
                    {
                        matched.Add(key);
                        matchedWeight += weight;
                    }
                    else
                    {
                        changed.Add(key);
                    }
                }
                else if (inCurr)
                {
                    // Added hardware component (e.g. secondary USB NIC or added disk)
                    // If network adapter, added adapters carry reduced penalty to avoid false drift triggers
                    double addedWeight = key.IndexOf("mac", StringComparison.OrdinalIgnoreCase) >= 0 ? (weight * 0.5) : weight;
                    totalWeight += addedWeight;
                    added.Add(key);
                }
                else
                {
                    // In previous but not in current
                    if (deniedOrUnavailable.Contains(key))
                    {
                        // Signal was unavailable or permission denied: do NOT penalize as physical hardware replacement
                        inaccessible.Add(key);
                    }
                    else
                    {
                        totalWeight += weight;
                        removed.Add(key);
                    }
                }
            }

            double similarity = totalWeight > 0 ? (matchedWeight / totalWeight) : 0.0;
            DeviceComparisonConfidence confidence;

            if (similarity >= 0.999)
            {
                confidence = DeviceComparisonConfidence.ExactMatch;
            }
            else if (similarity >= 0.75)
            {
                confidence = DeviceComparisonConfidence.HighConfidence;
            }
            else if (similarity >= 0.45)
            {
                confidence = DeviceComparisonConfidence.ModerateDrift;
            }
            else if (similarity >= 0.20)
            {
                confidence = DeviceComparisonConfidence.SignificantChange;
            }
            else
            {
                confidence = DeviceComparisonConfidence.DifferentDevice;
            }

            // Formulate human-readable diagnostic assessment
            var sb = new StringBuilder();
            if (changed.Count == 0 && added.Count == 0 && removed.Count == 0)
            {
                sb.Append("Hardware signals match previous fingerprint identically.");
            }
            else
            {
                if (changed.Count > 0)
                {
                    sb.Append("Changed signals: ").Append(string.Join(", ", changed)).Append(". ");
                }
                if (added.Count > 0)
                {
                    sb.Append("Added signals: ").Append(string.Join(", ", added)).Append(". ");
                }
                if (removed.Count > 0)
                {
                    sb.Append("Removed signals: ").Append(string.Join(", ", removed)).Append(". ");
                }
                if (inaccessible.Count > 0)
                {
                    sb.Append("Inaccessible/denied signals: ").Append(string.Join(", ", inaccessible)).Append(". ");
                }

                // Diagnose common patterns
                if (changed.All(s => s.IndexOf("mac", StringComparison.OrdinalIgnoreCase) >= 0) &&
                    removed.All(s => s.IndexOf("mac", StringComparison.OrdinalIgnoreCase) >= 0) &&
                    added.All(s => s.IndexOf("mac", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    sb.Append("Assessment: Network adapter modification detected; core hardware anchors remain unchanged.");
                }
                else if (changed.Any(s => s.IndexOf("drive", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("disk", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    sb.Append("Assessment: Primary storage change detected; motherboard and system identity preserved.");
                }
                else if (changed.Any(s => s.IndexOf("motherboard", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("baseboard", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("guid", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    sb.Append("Assessment: Core motherboard or system UUID change detected.");
                }
            }

            bool hasChanged = changed.Count > 0 || added.Count > 0 || removed.Count > 0;

            return new DeviceIdentityComparison(
                hasChanged: hasChanged,
                similarityScore: similarity,
                confidence: confidence,
                matchedSignals: matched,
                changedSignals: changed,
                addedSignals: added,
                removedSignals: removed,
                assessment: sb.ToString().Trim(),
                inaccessibleSignals: inaccessible);
        }
    }
}
