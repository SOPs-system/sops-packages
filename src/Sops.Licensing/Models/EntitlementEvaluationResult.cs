using System;

namespace Sops.Licensing.Models
{
    /// <summary>
    /// Outcome of an entitlement authorization check for a specific feature, module, or operation.
    /// </summary>
    public class EntitlementEvaluationResult
    {
        /// <summary>Gets whether the requested operation or feature is authorized.</summary>
        public bool IsAllowed { get; set; }

        /// <summary>Gets the requested entitlement key (e.g. "database.raw.read").</summary>
        public string EntitlementKey { get; set; } = string.Empty;

        /// <summary>Gets a human-readable reason describing why the check was allowed or denied.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Gets the rule or pattern that matched this request (e.g. "database.*" or exact match).</summary>
        public string? MatchedRule { get; set; }

        /// <summary>Gets remaining quota value if this entitlement has an associated numeric limit, or null.</summary>
        public long? RemainingQuota { get; set; }

        /// <summary>Creates an approved entitlement result.</summary>
        public static EntitlementEvaluationResult Allowed(string key, string rule, string reason = "Entitlement granted by license rule.", long? remainingQuota = null)
        {
            return new EntitlementEvaluationResult
            {
                IsAllowed = true,
                EntitlementKey = key,
                MatchedRule = rule,
                Reason = reason,
                RemainingQuota = remainingQuota
            };
        }

        /// <summary>Creates a denied entitlement result.</summary>
        public static EntitlementEvaluationResult Denied(string key, string reason, string? rule = null)
        {
            return new EntitlementEvaluationResult
            {
                IsAllowed = false,
                EntitlementKey = key,
                MatchedRule = rule,
                Reason = reason
            };
        }
    }
}
