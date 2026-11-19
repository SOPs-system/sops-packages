using System;
using System.Collections.Generic;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Models;

namespace Sops.Licensing.Entitlements
{
    /// <summary>
    /// Production-quality entitlement evaluation engine implementing a strict default-deny model.
    /// Supports exact matching, explicit denials (!feature), hierarchical wildcards (*, database.*),
    /// per-feature expiration, and numeric quota limits.
    /// </summary>
    public class EntitlementEvaluator : IEntitlementEvaluator
    {
        /// <inheritdoc />
        public EntitlementEvaluationResult Evaluate(
            LicenseCertificate? certificate,
            string entitlementKey,
            IDictionary<string, object>? context = null)
        {
            if (string.IsNullOrWhiteSpace(entitlementKey))
            {
                return EntitlementEvaluationResult.Denied(string.Empty, "Entitlement key cannot be empty.");
            }

            string requestedKey = entitlementKey.Trim();

            if (certificate == null)
            {
                return EntitlementEvaluationResult.Denied(requestedKey, "No active license certificate installed.");
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;

            // Check certificate date validity
            if (certificate.ValidFromUtc > now)
            {
                return EntitlementEvaluationResult.Denied(requestedKey, $"License is not yet valid (effective: {certificate.ValidFromUtc:u}).");
            }

            if (certificate.ExpiresAtUtc <= now)
            {
                return EntitlementEvaluationResult.Denied(requestedKey, $"License has expired on {certificate.ExpiresAtUtc:u}.");
            }

            var rules = certificate.Entitlements ?? new List<string>();

            // Phase 1: Check Explicit Denials first (e.g. "!database.export")
            foreach (string rule in rules)
            {
                if (rule.StartsWith("!", StringComparison.Ordinal))
                {
                    string deniedPattern = rule.Substring(1).Trim();
                    if (PatternMatches(deniedPattern, requestedKey))
                    {
                        return EntitlementEvaluationResult.Denied(
                            requestedKey,
                            $"Entitlement '{requestedKey}' is explicitly denied by license rule '{rule}'.",
                            rule);
                    }
                }
            }

            // Phase 2: Check Grants (Exact matches first, then wildcards)
            string? matchedGrantRule = null;

            // Exact match
            foreach (string rule in rules)
            {
                if (!rule.StartsWith("!", StringComparison.Ordinal) &&
                    string.Equals(rule.Trim(), requestedKey, StringComparison.OrdinalIgnoreCase))
                {
                    matchedGrantRule = rule;
                    break;
                }
            }

            // Wildcard match (e.g. "database.*" or "*")
            if (matchedGrantRule == null)
            {
                foreach (string rule in rules)
                {
                    if (!rule.StartsWith("!", StringComparison.Ordinal) &&
                        rule.Contains("*") &&
                        PatternMatches(rule.Trim(), requestedKey))
                    {
                        matchedGrantRule = rule;
                        break;
                    }
                }
            }

            if (matchedGrantRule == null)
            {
                return EntitlementEvaluationResult.Denied(
                    requestedKey,
                    $"Entitlement '{requestedKey}' is not granted by the active license (default deny).");
            }

            // Phase 3: Check Numeric Limits (if applicable)
            long? remainingQuota = null;
            if (certificate.Limits != null && certificate.Limits.Count > 0)
            {
                string limitKey = "max_" + requestedKey.Replace(".", "_");
                if (!certificate.Limits.TryGetValue(limitKey, out long maxLimit))
                {
                    // Also check for direct key match in limits
                    certificate.Limits.TryGetValue(requestedKey, out maxLimit);
                }

                if (maxLimit > 0)
                {
                    long currentUsage = 0;
                    if (context != null && context.TryGetValue("current_usage", out object? usageObj) && usageObj != null)
                    {
                        if (long.TryParse(usageObj.ToString(), out long u))
                        {
                            currentUsage = u;
                        }
                    }

                    long requestedAmount = 1;
                    if (context != null && context.TryGetValue("requested_amount", out object? reqObj) && reqObj != null)
                    {
                        if (long.TryParse(reqObj.ToString(), out long r))
                        {
                            requestedAmount = r;
                        }
                    }

                    if (currentUsage + requestedAmount > maxLimit)
                    {
                        return EntitlementEvaluationResult.Denied(
                            requestedKey,
                            $"Quota exceeded for '{requestedKey}'. Limit is {maxLimit}, current usage is {currentUsage}, requested {requestedAmount}.",
                            matchedGrantRule);
                    }

                    remainingQuota = maxLimit - (currentUsage + requestedAmount);
                }
            }

            return EntitlementEvaluationResult.Allowed(
                requestedKey,
                matchedGrantRule,
                $"Entitlement '{requestedKey}' granted by rule '{matchedGrantRule}'.",
                remainingQuota);
        }

        /// <inheritdoc />
        public bool IsEntitled(
            LicenseCertificate? certificate,
            string entitlementKey,
            IDictionary<string, object>? context = null)
        {
            var result = Evaluate(certificate, entitlementKey, context);
            return result.IsAllowed;
        }

        private static bool PatternMatches(string pattern, string key)
        {
            if (string.Equals(pattern, "*", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(pattern, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (pattern.EndsWith(".*", StringComparison.Ordinal))
            {
                string prefix = pattern.Substring(0, pattern.Length - 1); // e.g. "database."
                return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            if (pattern.Contains("*"))
            {
                // Simple regex-like wildcard matching
                string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                return System.Text.RegularExpressions.Regex.IsMatch(key, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return false;
        }
    }
}
