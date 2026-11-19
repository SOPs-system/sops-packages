using Sops.Cache.Abstractions;
using Xunit;

namespace Sops.Cache.Tests
{
    public class CacheResultTests
    {
        [Fact]
        public void Hit_WithValue_ReturnsIsHitTrueAndValue()
        {
            var result = CacheResult<string>.Hit("sample-value");

            Assert.True(result.IsHit);
            Assert.Equal("sample-value", result.Value);
        }

        [Fact]
        public void Hit_WithNullValue_DistinguishesFromMiss()
        {
            var hitNull = CacheResult<string>.Hit(null);
            var miss = CacheResult<string>.Miss();

            Assert.True(hitNull.IsHit);
            Assert.Null(hitNull.Value);

            Assert.False(miss.IsHit);
            Assert.Null(miss.Value);
            Assert.NotEqual(hitNull, miss);
        }

        [Fact]
        public void Miss_ReturnsIsHitFalseAndDefaultValue()
        {
            var intMiss = CacheResult<int>.Miss();

            Assert.False(intMiss.IsHit);
            Assert.Equal(0, intMiss.Value);
        }

        [Fact]
        public void EqualsAndHashCode_WorkCorrectly()
        {
            var res1 = CacheResult<string>.Hit("hello");
            var res2 = CacheResult<string>.Hit("hello");
            var res3 = CacheResult<string>.Hit("world");

            Assert.Equal(res1, res2);
            Assert.Equal(res1.GetHashCode(), res2.GetHashCode());
            Assert.NotEqual(res1, res3);
        }
    }
}
