using System;
using System.Collections.Generic;

namespace Sops.Device.Models
{
    /// <summary>
    /// Confidence classification of hardware identity comparison between two fingerprints.
    /// </summary>
    public enum DeviceComparisonConfidence
    {
        /// <summary>All signals match identically (100% similarity).</summary>
        ExactMatch = 0,

        /// <summary>Minor non-critical signal changes (e.g. single NIC replaced, similarity >= 80%).</summary>
        HighConfidence = 1,

        /// <summary>Moderate component drift (e.g. multiple secondary components changed, similarity 50%-79%).</summary>
        ModerateDrift = 2,

        /// <summary>Significant hardware changes detected (e.g. motherboard or drive replaced, similarity 20%-49%).</summary>
        SignificantChange = 3,

        /// <summary>Fundamentally different machine or cloned environment (similarity &lt; 20%).</summary>
        DifferentDevice = 4,

        /// <summary>
        /// Fingerprints use differing cryptographic algorithms, key scopes, or versions (e.g. v1 unkeyed SHA-256 vs v2 keyed HMAC)
        /// and cannot be mathematically compared without live raw hardware signals.
        /// </summary>
        Indeterminate = 5
    }

    /// <summary>
    /// Structured comparison result between a previously recorded device fingerprint and the current device state.
    /// Distinguishes routine hardware maintenance (e.g. network card replacement) from machine cloning.
    /// </summary>
    public sealed class DeviceIdentityComparison
    {
        /// <summary>Whether any detectable hardware or system signal differed from the previous fingerprint.</summary>
        public bool HasChanged { get; }

        /// <summary>Calculated similarity score between 0.0 (completely distinct) and 1.0 (identical).</summary>
        public double SimilarityScore { get; }

        /// <summary>Confidence classification based on weighted signal importance.</summary>
        public DeviceComparisonConfidence Confidence { get; }

        /// <summary>Signals that matched identically in both fingerprints.</summary>
        public IReadOnlyList<string> MatchedSignals { get; }

        /// <summary>Signals present in both fingerprints whose values differed.</summary>
        public IReadOnlyList<string> ChangedSignals { get; }

        /// <summary>Signals present in the current fingerprint but absent in the previous fingerprint.</summary>
        public IReadOnlyList<string> AddedSignals { get; }

        /// <summary>Signals present in the previous fingerprint but missing or unavailable in the current fingerprint.</summary>
        public IReadOnlyList<string> RemovedSignals { get; }

        /// <summary>Signals that were unavailable or denied access rather than confirmed physical hardware changes.</summary>
        public IReadOnlyList<string> InaccessibleSignals { get; }

        /// <summary>Human-readable diagnostic assessment of the hardware changes.</summary>
        public string Assessment { get; }

        /// <summary>Timestamp in UTC when the comparison was performed.</summary>
        public DateTimeOffset ComparedAtUtc { get; }

        public DeviceIdentityComparison(
            bool hasChanged,
            double similarityScore,
            DeviceComparisonConfidence confidence,
            IReadOnlyList<string> matchedSignals,
            IReadOnlyList<string> changedSignals,
            IReadOnlyList<string> addedSignals,
            IReadOnlyList<string> removedSignals,
            string assessment,
            IReadOnlyList<string>? inaccessibleSignals = null,
            DateTimeOffset? comparedAtUtc = null)
        {
            HasChanged = hasChanged;
            SimilarityScore = Math.Max(0.0, Math.Min(1.0, similarityScore));
            Confidence = confidence;
            MatchedSignals = matchedSignals ?? Array.Empty<string>();
            ChangedSignals = changedSignals ?? Array.Empty<string>();
            AddedSignals = addedSignals ?? Array.Empty<string>();
            RemovedSignals = removedSignals ?? Array.Empty<string>();
            InaccessibleSignals = inaccessibleSignals ?? Array.Empty<string>();
            Assessment = assessment ?? string.Empty;
            ComparedAtUtc = comparedAtUtc ?? DateTimeOffset.UtcNow;
        }

        public static DeviceIdentityComparison ExactMatch(IReadOnlyList<string> matchedSignals) =>
            new DeviceIdentityComparison(false, 1.0, DeviceComparisonConfidence.ExactMatch, matchedSignals, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), "Hardware signals match previous fingerprint identically.");

        public static DeviceIdentityComparison IncomparableMigration(int prevVersion, int currVersion, string assessment) =>
            new DeviceIdentityComparison(
                hasChanged: true,
                similarityScore: 0.0,
                confidence: DeviceComparisonConfidence.Indeterminate,
                matchedSignals: Array.Empty<string>(),
                changedSignals: Array.Empty<string>(),
                addedSignals: Array.Empty<string>(),
                removedSignals: Array.Empty<string>(),
                assessment: assessment);
    }
}
