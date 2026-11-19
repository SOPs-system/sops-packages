using System;
using System.Collections.Generic;

namespace Sops.Cache.Abstractions
{
    /// <summary>
    /// Represents the result of a cache retrieval operation, explicitly distinguishing between a cache miss and a cached null/default value.
    /// </summary>
    /// <typeparam name="T">The type of the cached item.</typeparam>
    public sealed class CacheResult<T> : IEquatable<CacheResult<T>>
    {
        private static readonly CacheResult<T> MissResult = new CacheResult<T>(false, default);

        /// <summary>
        /// Gets a value indicating whether the item was found in the cache.
        /// </summary>
        public bool IsHit { get; }

        /// <summary>
        /// Gets the cached value if <see cref="IsHit"/> is <c>true</c>; otherwise, the default value of <typeparamref name="T"/>.
        /// </summary>
        public T? Value { get; }

        private CacheResult(bool isHit, T? value)
        {
            IsHit = isHit;
            Value = value;
        }

        /// <summary>
        /// Creates a cache hit result containing the retrieved value.
        /// </summary>
        /// <param name="value">The cached value.</param>
        /// <returns>A cache hit result.</returns>
        public static CacheResult<T> Hit(T? value) => new CacheResult<T>(true, value);

        /// <summary>
        /// Gets a cache miss result indicating the requested key was not present in the cache.
        /// </summary>
        /// <returns>A cache miss result.</returns>
        public static CacheResult<T> Miss() => MissResult;

        /// <inheritdoc />
        public bool Equals(CacheResult<T>? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return IsHit == other.IsHit && EqualityComparer<T?>.Default.Equals(Value, other.Value);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as CacheResult<T>);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + IsHit.GetHashCode();
                if (Value != null)
                {
                    hash = hash * 31 + EqualityComparer<T?>.Default.GetHashCode(Value);
                }
                return hash;
            }
        }
    }
}
