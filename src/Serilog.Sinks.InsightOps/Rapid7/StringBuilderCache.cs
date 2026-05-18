using System.Text;

namespace Serilog.Sinks.InsightIDR.Rapid7
{
    internal static class StringBuilderCache
    {
        internal const int MaxBuilderSize = 65360;

        [ThreadStatic]
        private static StringBuilder? CachedInstance;

        public static StringBuilder Acquire(int capacity = MaxBuilderSize)
        {
            if (capacity <= MaxBuilderSize)
            {
                var sb = CachedInstance;
                if (sb != null)
                {
                    // Avoid StringBuilder block fragmentation by getting a new StringBuilder
                    // when the requested size is larger than the current capacity
                    if (capacity <= sb.Capacity)
                    {
                        CachedInstance = null;
                        sb.Clear();
                        return sb;
                    }
                }
            }

            return new StringBuilder(capacity);
        }

        public static string GetStringAndRelease(StringBuilder sb)
        {
            var result = sb.ToString();
            Release(sb);

            return result;
        }

        public static void Release(StringBuilder sb)
        {
            if (sb?.Capacity <= MaxBuilderSize)
            {
                CachedInstance = sb;
            }
        }
    }
}
