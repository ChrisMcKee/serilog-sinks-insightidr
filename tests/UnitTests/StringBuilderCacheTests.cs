using System.Text;
using Serilog.Sinks.InsightIDR.Rapid7;

namespace UnitTests;

public class StringBuilderCacheTests
{
    [Fact]
    public void Acquire_WhenCacheEmpty_ReturnsNewInstance()
    {
        StringBuilderCache.Release(null!); // ensure no stale cache
        var sb = StringBuilderCache.Acquire();
        Assert.NotNull(sb);
    }

    [Fact]
    public void Acquire_WhenCachedAndCapacityFits_ReturnsCachedInstance()
    {
        var first = StringBuilderCache.Acquire(256);
        first.Append("hello");
        StringBuilderCache.Release(first);

        var second = StringBuilderCache.Acquire(256);
        Assert.Same(first, second);
    }

    [Fact]
    public void Acquire_ClearsContentBeforeReturning()
    {
        var first = StringBuilderCache.Acquire(256);
        first.Append("stale data");
        StringBuilderCache.Release(first);

        var second = StringBuilderCache.Acquire(256);
        Assert.Equal(0, second.Length);
    }

    [Fact]
    public void Acquire_WhenRequestedCapacityExceedsMax_ReturnsNewInstance()
    {
        var first = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
        StringBuilderCache.Release(first);

        // requesting more than MaxBuilderSize should bypass the cache
        var oversized = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize + 1);
        Assert.NotSame(first, oversized);
    }

    [Fact]
    public void Acquire_WhenCachedCapacityTooSmall_ReturnsNewInstance()
    {
        // seed a small-capacity builder into the cache
        var small = new StringBuilder(16);
        StringBuilderCache.Release(small);

        // requesting more than cached capacity should avoid reuse (fragmentation avoidance)
        var larger = StringBuilderCache.Acquire(1024);
        Assert.NotSame(small, larger);
    }

    [Fact]
    public void GetStringAndRelease_ReturnsCorrectString()
    {
        var sb = StringBuilderCache.Acquire();
        sb.Append("test value");

        var result = StringBuilderCache.GetStringAndRelease(sb);
        Assert.Equal("test value", result);
    }

    [Fact]
    public void GetStringAndRelease_ReleasesBuilderBackToCache()
    {
        var sb = StringBuilderCache.Acquire(256);
        StringBuilderCache.GetStringAndRelease(sb);

        var reacquired = StringBuilderCache.Acquire(256);
        Assert.Same(sb, reacquired);
    }

    [Fact]
    public void Release_WhenCapacityExceedsMax_DoesNotCache()
    {
        var oversized = new StringBuilder(StringBuilderCache.MaxBuilderSize + 1);
        StringBuilderCache.Release(oversized);

        // The next Acquire should not return the oversized instance
        var next = StringBuilderCache.Acquire(256);
        Assert.NotSame(oversized, next);
    }
}
