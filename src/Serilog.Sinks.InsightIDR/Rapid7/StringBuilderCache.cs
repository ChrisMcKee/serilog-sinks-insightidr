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
            if (capacity > MaxBuilderSize) return new StringBuilder(capacity);
            var sb = CachedInstance;
            if (sb == null || capacity > sb.Capacity) return new StringBuilder(capacity);
            CachedInstance = null;
            sb.Clear();
            return sb;
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
