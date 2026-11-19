using System;
using System.Linq;
using Sops.DataLayer.Endpoints;
using Xunit;

namespace Sops.DataLayer.Tests
{
    public class EndpointRegistryTests
    {
        [Fact]
        public void Registry_Contains_Standard_Sops_Endpoints()
        {
            var registry = new EndpointRegistry();
            var all = registry.All();

            Assert.NotEmpty(all);
            Assert.True(registry.TryGet("system.health", out var health));
            Assert.NotNull(health);
            Assert.Equal("/health", health!.Route);

            Assert.True(registry.TryGet("auth.login", out var login));
            Assert.NotNull(login);
            Assert.Equal("/rpc/auth/login", login!.Route);

            Assert.True(registry.TryGet("drugs.search", out var drugs));
            Assert.NotNull(drugs);
            Assert.Equal(TransportMode.Rpc, drugs!.Transport);

            Assert.True(registry.TryGet("sales.complete", out var sale));
            Assert.NotNull(sale);
            Assert.Equal(SensitivityClassification.Financial, sale!.Sensitivity);
        }

        [Fact]
        public void Register_Duplicate_Throws_InvalidOperationException()
        {
            var registry = new EndpointRegistry();
            var duplicate = new EndpointDefinition("system.health", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", "/health");

            Assert.Throws<InvalidOperationException>(() => registry.Register(duplicate));
        }

        [Theory]
        [InlineData("http://evil.com/api")]
        [InlineData("https://attacker.org/steal")]
        [InlineData("ftp://internal/server")]
        public void Register_With_Absolute_Scheme_Throws_ArgumentException_SSRF_Defense(string maliciousRoute)
        {
            var registry = new EndpointRegistry();
            Assert.Throws<ArgumentException>(() =>
                registry.Register(new EndpointDefinition("malicious.endpoint", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", maliciousRoute)));
        }

        [Theory]
        [InlineData("/api/../../windows/system32")]
        [InlineData("/rpc/../secrets")]
        public void Register_With_Path_Traversal_Throws_ArgumentException(string traversalRoute)
        {
            var registry = new EndpointRegistry();
            Assert.Throws<ArgumentException>(() =>
                registry.Register(new EndpointDefinition("traversal.endpoint", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", traversalRoute)));
        }

        [Fact]
        public void EndpointResolver_Resolves_Local_And_Global_Correctly()
        {
            var options = new Configuration.DataLayerOptions
            {
                LocalServerAddress = new Uri("http://127.0.0.1:5000"),
                GlobalApiAddress = new Uri("https://api.sops.pharmacy")
            };
            var resolver = new EndpointResolver(options);

            var localEp = new EndpointDefinition("local.test", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", "/api/test");
            var globalEp = new EndpointDefinition("global.test", BackendTarget.GlobalApi, TransportMode.HttpRest, "GET", "/api/v1/status");

            var localUri = resolver.ResolveUri(localEp);
            var globalUri = resolver.ResolveUri(globalEp);

            Assert.Equal("http://127.0.0.1:5000/api/test", localUri.ToString());
            Assert.Equal("https://api.sops.pharmacy/api/v1/status", globalUri.ToString());
        }

        [Fact]
        public void EndpointResolver_Appends_Query_Parameters()
        {
            var options = new Configuration.DataLayerOptions
            {
                LocalServerAddress = new Uri("http://127.0.0.1:5000")
            };
            var resolver = new EndpointResolver(options);
            var ep = new EndpointDefinition("search.test", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", "/api/search");

            var query = new System.Collections.Generic.Dictionary<string, string>
            {
                { "q", "panadol" },
                { "limit", "10" }
            };

            var uri = resolver.ResolveUri(ep, query);
            Assert.Contains("q=panadol", uri.Query);
            Assert.Contains("limit=10", uri.Query);
        }
    }
}
