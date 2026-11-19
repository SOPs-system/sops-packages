using System;
using System.Collections.Generic;
using Sops.Licensing.Entitlements;
using Sops.Licensing.Models;
using Xunit;

namespace Sops.Licensing.Tests
{
    public class EntitlementTests
    {
        private readonly EntitlementEvaluator _evaluator = new EntitlementEvaluator();

        private LicenseCertificate CreateActiveCertificate(params string[] entitlements)
        {
            return new LicenseCertificate
            {
                LicenseId = "lic-test-01",
                ProductId = "SOPs.App",
                ValidFromUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
                Entitlements = new List<string>(entitlements)
            };
        }

        [Fact]
        public void Evaluate_DefaultDeny_WhenNoRulesMatch()
        {
            var cert = CreateActiveCertificate("app.login", "pos.access");

            var result = _evaluator.Evaluate(cert, "database.raw.read");
            Assert.False(result.IsAllowed);
            Assert.Contains("default deny", result.Reason);
        }

        [Fact]
        public void Evaluate_ExactMatch_GrantsEntitlement()
        {
            var cert = CreateActiveCertificate("app.login", "database.raw.read");

            var result = _evaluator.Evaluate(cert, "database.raw.read");
            Assert.True(result.IsAllowed);
            Assert.Equal("database.raw.read", result.MatchedRule);
        }

        [Theory]
        [InlineData("database.connect")]
        [InlineData("database.raw.read")]
        [InlineData("database.record.create")]
        [InlineData("database.record.update")]
        [InlineData("database.record.delete")]
        [InlineData("database.export")]
        public void Evaluate_ScopedWildcard_GrantsAllSubFeatures(string requestedFeature)
        {
            var cert = CreateActiveCertificate("app.login", "database.*");

            var result = _evaluator.Evaluate(cert, requestedFeature);
            Assert.True(result.IsAllowed);
            Assert.Equal("database.*", result.MatchedRule);
        }

        [Fact]
        public void Evaluate_ExplicitDenial_OverridesWildcardGrant()
        {
            // Grants all database features, but explicitly forbids raw database read and export
            var cert = CreateActiveCertificate("database.*", "!database.raw.read", "!database.export");

            // database.connect should be allowed by wildcard
            var connectResult = _evaluator.Evaluate(cert, "database.connect");
            Assert.True(connectResult.IsAllowed);

            // database.raw.read must be explicitly denied
            var rawReadResult = _evaluator.Evaluate(cert, "database.raw.read");
            Assert.False(rawReadResult.IsAllowed);
            Assert.Contains("explicitly denied", rawReadResult.Reason);
            Assert.Equal("!database.raw.read", rawReadResult.MatchedRule);

            // database.export must be explicitly denied
            var exportResult = _evaluator.Evaluate(cert, "database.export");
            Assert.False(exportResult.IsAllowed);
            Assert.Contains("explicitly denied", exportResult.Reason);
        }

        [Fact]
        public void Evaluate_GlobalWildcard_GrantsArbitraryFeatures()
        {
            var cert = CreateActiveCertificate("*", "!dangerous.operation");

            Assert.True(_evaluator.IsEntitled(cert, "app.login"));
            Assert.True(_evaluator.IsEntitled(cert, "custom.anything"));
            Assert.True(_evaluator.IsEntitled(cert, "inventory.manage"));

            Assert.False(_evaluator.IsEntitled(cert, "dangerous.operation"));
        }

        [Fact]
        public void Evaluate_NumericQuotaLimits_EnforcesMaxAllowed()
        {
            var cert = CreateActiveCertificate("pos.terminal");
            cert.Limits["max_pos_terminal"] = 5;

            // Usage 3 + requested 1 = 4 <= 5 -> Allowed
            var ctxAllowed = new Dictionary<string, object>
            {
                { "current_usage", 3 },
                { "requested_amount", 1 }
            };
            var res1 = _evaluator.Evaluate(cert, "pos.terminal", ctxAllowed);
            Assert.True(res1.IsAllowed);
            Assert.Equal(1, res1.RemainingQuota);

            // Usage 5 + requested 1 = 6 > 5 -> Denied
            var ctxExceeded = new Dictionary<string, object>
            {
                { "current_usage", 5 },
                { "requested_amount", 1 }
            };
            var res2 = _evaluator.Evaluate(cert, "pos.terminal", ctxExceeded);
            Assert.False(res2.IsAllowed);
            Assert.Contains("Quota exceeded", res2.Reason);
        }

        [Fact]
        public void Evaluate_ExpiredCertificate_DeniesAllEntitlements()
        {
            var cert = new LicenseCertificate
            {
                ValidFromUtc = DateTimeOffset.UtcNow.AddDays(-60),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1), // Expired yesterday
                Entitlements = new List<string> { "*" }
            };

            var res = _evaluator.Evaluate(cert, "app.login");
            Assert.False(res.IsAllowed);
            Assert.Contains("expired", res.Reason);
        }
    }
}
