using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sops.Database.Abstractions;
using Sops.Database.Documents;
using Xunit;

namespace Sops.Database.Tests
{
    public class DocumentContractTests
    {
        private record TestUser(string Name, int Age, bool Active);

        [Fact]
        public async Task DocumentCollection_CRUD_OperationsWorkCorrectly()
        {
            using var provider = new InMemoryDocumentDatabaseProvider();
            IDocumentCollection<TestUser> collection = provider.GetCollection<TestUser>("users");

            Assert.Equal("users", collection.CollectionName);
            Assert.Equal(0, await collection.CountAsync());

            // 1. Insert
            await collection.InsertAsync("u1", new TestUser("Alice", 30, true));
            Assert.Equal(1, await collection.CountAsync());

            // 2. Duplicate insert throws
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                collection.InsertAsync("u1", new TestUser("Alice Twin", 30, true)));

            // 3. GetById
            TestUser? fetched = await collection.GetByIdAsync("u1");
            Assert.NotNull(fetched);
            Assert.Equal("Alice", fetched.Name);

            // 4. Upsert (update existing)
            await collection.UpsertAsync("u1", new TestUser("Alice Smith", 31, true));
            TestUser? updated = await collection.GetByIdAsync("u1");
            Assert.NotNull(updated);
            Assert.Equal("Alice Smith", updated.Name);
            Assert.Equal(31, updated.Age);

            // 5. Upsert (insert new)
            await collection.UpsertAsync("u2", new TestUser("Bob", 40, false));
            Assert.Equal(2, await collection.CountAsync());

            // 6. Find
            IReadOnlyList<TestUser> activeUsers = await collection.FindAsync(u => u.Active);
            Assert.Single(activeUsers);
            Assert.Equal("Alice Smith", activeUsers[0].Name);

            // 7. Delete
            bool deleted = await collection.DeleteAsync("u1");
            Assert.True(deleted);
            Assert.Equal(1, await collection.CountAsync());

            bool deletedAgain = await collection.DeleteAsync("u1");
            Assert.False(deletedAgain);

            TestUser? deletedDoc = await collection.GetByIdAsync("u1");
            Assert.Null(deletedDoc);
        }

        [Fact]
        public async Task StronglyTypedKeys_WorkCorrectly()
        {
            using var provider = new InMemoryDocumentDatabaseProvider();
            IDocumentCollection<TestUser, Guid> collection = provider.GetCollection<TestUser, Guid>("guid_users");

            var id = Guid.NewGuid();
            await collection.InsertAsync(id, new TestUser("Charlie", 25, true));

            TestUser? fetched = await collection.GetByIdAsync(id);
            Assert.NotNull(fetched);
            Assert.Equal("Charlie", fetched.Name);
        }

        [Fact]
        public async Task HealthCheck_ReturnsHealthy()
        {
            using var provider = new InMemoryDocumentDatabaseProvider();
            DatabaseHealthResult health = await provider.CheckHealthAsync();

            Assert.True(health.IsHealthy);
            Assert.Contains("InMemoryDocument provider is healthy", health.Description);
        }

        [Fact]
        public void DisposedDocumentProvider_ThrowsObjectDisposedException()
        {
            var provider = new InMemoryDocumentDatabaseProvider();
            provider.Dispose();

            Assert.Throws<ObjectDisposedException>(() => provider.GetCollection<TestUser>("users"));
        }
    }
}
