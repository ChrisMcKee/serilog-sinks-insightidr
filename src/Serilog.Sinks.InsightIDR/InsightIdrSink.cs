using System.Buffers;
using System.Text;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.InsightIDR.Rapid7;

namespace Serilog.Sinks.InsightIDR
{
    /// <summary>
    /// Formats and ships batches of log events to Rapid7 InsightIDR (or a DataHub instance) over
    /// a persistent TCP/TLS connection. Batching, retry/backoff on transport failure, and shutdown
    /// flushing are all handled by Serilog core's native batching sink (registered via
    /// <c>LoggerSinkConfiguration.Sink(IBatchedLogEventSink, BatchingOptions, ...)</c>) — this class
    /// only owns formatting and the connection.
    /// </summary>
    public sealed class InsightIdrSink : IBatchedLogEventSink, IAsyncDisposable
    {
        [ThreadStatic]
        private static StringWriter? CachedWriter;

        private static readonly UTF8Encoding Utf8 = new(false, true);

        private readonly ITextFormatter _textFormatter;
        private readonly bool _useDataHub;
        private readonly string _tokenPrefix;
        private readonly string _messagePrefix;
        private readonly Func<IInsightConnection> _connectionFactory;
        private IInsightConnection? _connection;

        /// <summary>
        /// The insightOps sink → a service which sends log messages to insightOps.
        /// </summary>
        /// <param name="config">insightOps settings.</param>
        /// <param name="textFormatter">Formats log events.</param>
        public InsightIdrSink(InsightIdrSinkSettings config, ITextFormatter textFormatter)
            : this(config, textFormatter, connectionFactory: null)
        {
        }

        internal InsightIdrSink(InsightIdrSinkSettings config, ITextFormatter textFormatter, Func<IInsightConnection>? connectionFactory)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _textFormatter = textFormatter;

            ValidateToken(config.Token);
            ValidateRegion(config.Region);

            _useDataHub = config.IsUsingDataHub;
            _tokenPrefix = _useDataHub ? string.Empty : config.Token;
            _messagePrefix = BuildMessagePrefix(config);

            _connectionFactory = connectionFactory
                ?? (() => new InsightTcpClient(config.UseSsl, config.IsUsingDataHub, config.DataHubAddress, config.DataHubPort, config.Region));
        }

        private static string BuildMessagePrefix(InsightIdrSinkSettings config)
        {
            var prefix = config.LogId != string.Empty ? config.LogId + " " : string.Empty;
            if (!config.LogHostname) return prefix;

            var hostName = config.HostName;
            if (string.IsNullOrEmpty(hostName))
            {
                hostName = Environment.MachineName;
            }
            else if (!Rapid7LineFormatter.CheckIfHostNameValid(hostName))
            {
                // User-defined host name contains prohibited characters — send without it.
                return prefix;
            }

            return prefix + "HostName=" + hostName + " ";
        }

        public Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch)
        {
            if (batch is null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            return EmitBatchCoreAsync(batch);
        }

        private async Task EmitBatchCoreAsync(IReadOnlyCollection<LogEvent> batch)
        {
            foreach (var logEvent in batch)
            {
                await WriteLogEventAsync(logEvent).ConfigureAwait(false);
            }
        }

        // OnEmptyBatchAsync intentionally not overridden — Serilog.Core.IBatchedLogEventSink's default
        // implementation (no-op) is exactly what we want.

        private async Task WriteLogEventAsync(LogEvent logEvent)
        {
            var writer = GetWriter();
            _textFormatter.Format(logEvent, writer);
            var formatted = writer.GetStringBuilder().ToString();

            foreach (var chunk in Rapid7LineFormatter.ChunkMessage(formatted))
            {
                await WriteLineAsync(chunk).ConfigureAwait(false);
            }
        }

        private async Task WriteLineAsync(string chunk)
        {
            var logLine = StringBuilderCache.Acquire();
            if (!_useDataHub) logLine.Append(_tokenPrefix);
            if (_messagePrefix.Length > 0) logLine.Append(_messagePrefix);

            Rapid7LineFormatter.AppendWithNewlineReplacement(logLine, chunk);
            logLine.Append(Rapid7LineFormatter.NixNewLine);

            var text = StringBuilderCache.GetStringAndRelease(logLine);
            var byteCount = Utf8.GetByteCount(text);
            var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                var written = Utf8.GetBytes(text, buffer);
                await WriteWithReconnectAsync(buffer.AsMemory(0, written)).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task WriteWithReconnectAsync(ReadOnlyMemory<byte> payload)
        {
            _connection ??= _connectionFactory();

            try
            {
                await _connection.EnsureConnectedAsync().ConfigureAwait(false);
                await _connection.WriteAsync(payload).ConfigureAwait(false);
            }
            catch
            {
                // Force a fresh connection on the next attempt. Let the batch's exception propagate so
                // the batching infrastructure's own retry/backoff (FailureAwareBatchScheduler) handles
                // timing instead of us reimplementing that here.
                _connection.Dispose();
                _connection = null;
                throw;
            }
        }

        private static StringWriter GetWriter()
        {
            var writer = CachedWriter;
            if (writer is null)
                return CachedWriter = new StringWriter(new StringBuilder(256));
            writer.GetStringBuilder().Clear();
            return writer;
        }

        public ValueTask DisposeAsync()
        {
            _connection?.Dispose();
            _connection = null;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// The Token should be a GUID. Validated eagerly (rather than deferred to the first failed
        /// send) so misconfiguration fails fast and loudly instead of silently dropping every event.
        /// </summary>
        private static void ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("The InsightOps Token (which is a Guid) is required. Otherwise, how else are logs going to be sent?");
            }

            var isGuid = Guid.TryParse(token, out _);
            if (!isGuid)
            {
                throw new Exception($"Provided Token '{token}' is not a valid Guid");
            }
        }

        private static void ValidateRegion(string region)
        {
            if (string.IsNullOrWhiteSpace(region))
            {
                throw new Exception("A region (e.g. 'eu', 'us') is required.");
            }
        }
    }
}
