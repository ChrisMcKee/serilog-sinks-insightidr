using System.Text;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.InsightIDR.Rapid7
{
    /// <summary>
    /// Pure wire-format helpers for building Rapid7 InsightIDR log lines: newline normalisation,
    /// oversized-message chunking, and host name validation. Has no knowledge of the transport.
    /// </summary>
    internal static partial class Rapid7LineFormatter
    {
        // Limit on individual log length i.e. 2^16
        internal const int LogLengthLimit = 65536;

        // Limit on recursion for splitting long logs into chunks.
        internal const int RecursionLimit = 32;

        /** Linux new-line */
        internal const char NixNewLine = '\n';

        // Unicode line separator character (U+2028), used in place of embedded \r/\n so a chunk
        // remains a single wire record.
        internal static readonly string LineSeparator = char.ConvertFromUtf32(0x2028);

        private static readonly char[] TrimChars = ['\r', '\n'];

        // Restricted symbols that should not appear in host name.
        // See http://support.microsoft.com/kb/228275/en-us for details.
        private static readonly Regex ForbiddenHostNameChars = ForbiddenHostNameCharsRegex();

        internal static bool CheckIfHostNameValid(string hostName) => !ForbiddenHostNameChars.IsMatch(hostName);

        internal static void AppendWithNewlineReplacement(StringBuilder sb, string source)
        {
            var span = source.AsSpan();
            int start = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] is not ('\r' or '\n')) continue;
                if (start < i) sb.Append(span[start..i]);
                sb.Append(LineSeparator);
                if (span[i] == '\r' && i + 1 < span.Length && span[i + 1] == '\n')
                    i++;
                start = i + 1;
            }
            if (start < span.Length) sb.Append(span[start..]);
        }

        /// <summary>
        /// Splits an overlong message into up to <see cref="RecursionLimit"/> chunks of at most
        /// <see cref="LogLengthLimit"/> characters. If the message still doesn't fit after
        /// <see cref="RecursionLimit"/> chunks have been produced, the remainder is silently dropped.
        /// </summary>
        internal static IReadOnlyList<string> ChunkMessage(string message)
        {
            var chunks = new List<string>();
            var remaining = message.TrimEnd(TrimChars);
            var limit = RecursionLimit;

            while (true)
            {
                if (limit == 0) return chunks;

                if (remaining.Length > LogLengthLimit)
                {
                    chunks.Add(remaining[..LogLengthLimit]);
                    remaining = remaining[LogLengthLimit..];
                    limit--;
                    continue;
                }

                chunks.Add(remaining);
                return chunks;
            }
        }

        [GeneratedRegex(@"[/\\\[\]\""\:\;\|\<\>\+\=\,\?\* _]{1,}", RegexOptions.Compiled, 300)]
        private static partial Regex ForbiddenHostNameCharsRegex();
    }
}
