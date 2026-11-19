# Sops.Cache

`Sops.Cache` is a production-quality, all-in-one caching SDK providing unified abstractions, high-performance in-memory caching, and robust Redis distributed caching for SOPs applications.

---

## Key Features

- **Unified SDK**: Both in-memory and Redis distributed cache providers are built directly into `Sops.Cache` with zero separate provider packages required.
- **Provider-Independent Abstractions**: `ICacheService` provides a unified contract for typed caching operations across in-memory and distributed backends.
- **Accurate Cache Semantics**: `CacheResult<T>` distinguishes between cache misses (`IsHit = false`) and stored null/default values (`IsHit = true, Value = null`).
- **Cache-Aside Pattern**: `GetOrCreateAsync<T>` extension helper executing factories strictly on cache misses and never caching exceptions.
- **Key Namespacing**: Transparent support for global key prefixes across cache providers.
- **Backward-Compatible Namespaces**: Preserves `Sops.Cache.Memory` and `Sops.Cache.Redis` namespaces for complete backward compatibility.
- **Resilient Connection Handling**: Single multiplexer reuse, configurable timeouts, failover tolerance, active health checks (`CheckHealthAsync`), and sensitive credential masking.

---

## Installation

```bash
# All-in-one caching package containing abstractions, in-memory, and Redis providers
dotnet add package Sops.Cache
```

---

## Quickstart

### 1. In-Memory Caching Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sops.Cache.Abstractions;
using Sops.Cache.Extensions;
using Sops.Cache.Memory.Extensions;

var services = new ServiceCollection();

// Configure SOPs cache with In-Memory provider
services.AddSopsCache(options =>
{
    options.Provider = "Memory";
    options.KeyPrefix = "sops";
    options.DefaultTtlSeconds = 300;
});

// Optional: fine-tune memory cache settings (size limits, compaction)
services.AddSopsMemoryCache(options =>
{
    options.SizeLimit = 10000;
    options.CompactionPercentage = 0.05;
});

using var provider = services.BuildServiceProvider();
var cache = provider.GetRequiredService<ICacheService>();
```

### 2. Redis Distributed Caching Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sops.Cache.Abstractions;
using Sops.Cache.Extensions;
using Sops.Cache.Redis.Extensions;

var services = new ServiceCollection();

// Configure SOPs cache with Redis provider
services.AddSopsCache(options =>
{
    options.Provider = "Redis";
    options.KeyPrefix = "sops:pos";
    options.DefaultTtlSeconds = 600;
});

// Register Redis connection settings
services.AddSopsRedisCache(options =>
{
    options.ConnectionString = "redis-server:6379,password=SecretToken";
    options.Database = 0;
    options.ConnectTimeoutMilliseconds = 5000;
    options.SyncTimeoutMilliseconds = 5000;
});

using var provider = services.BuildServiceProvider();
var cache = provider.GetRequiredService<ICacheService>();
```

---

## Usage Examples

### Basic Cache Operations

```csharp
using Sops.Cache.Abstractions;

// Store value with absolute expiration
await cache.SetAsync("user:101", new UserProfile { Name = "Jane Doe" }, CacheEntryOptions.FromMinutes(10));

// Retrieve value distinguishing hit from miss
CacheResult<UserProfile> result = await cache.GetAsync<UserProfile>("user:101");
if (result.IsHit)
{
    Console.WriteLine($"Found: {result.Value?.Name}");
}

// Check existence and remove
bool exists = await cache.ExistsAsync("user:101");
bool removed = await cache.RemoveAsync("user:101");
```

### Cache-Aside Pattern (`GetOrCreateAsync`)

```csharp
using Sops.Cache.Extensions;

var profile = await cache.GetOrCreateAsync(
    "user:profile:101",
    async cancellationToken =>
    {
        // Executed ONLY on cache miss
        return await userRepository.FetchByIdAsync(101, cancellationToken);
    },
    CacheEntryOptions.FromMinutes(30));
```

---

## Target Frameworks

- `netstandard2.0` (compatible with .NET Framework 4.8 and modern .NET 8 / 9+)
