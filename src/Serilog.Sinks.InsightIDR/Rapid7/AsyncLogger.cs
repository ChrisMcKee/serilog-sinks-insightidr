using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Serilog.Debugging;

namespace Serilog.Sinks.InsightIDR.Rapid7
{
     public sealed class AsyncLogger
    {
        // Size of the internal event queue.
        private const int QueueSize = 32768;

        // Limit on individual log length i.e. 2^16
        private const int LogLengthLimit = 65536;

        // Limit on recursion for appending long logs to queue
        private const int RecursionLimit = 32;

        // Minimal delay between attempts to reconnect in milliseconds.
        private const int MinDelay = 100;

        // Maximal delay between attempts to reconnect in milliseconds.
        private const int MaxDelay = 10000;

        // Appender signature - used for debugging messages.
        private const string InternalLogPrefix = "R7Insight: {0}";

        // Error message displayed when invalid token is detected.
        private const string InvalidTokenMessage = "\n\nIt appears your log token value is invalid or missing.\n\n";

        // Error message displayed when queue overflow occurs.
        private const string QueueOverflowMessage = "\n\nInsight logger buffer queue overflow. Message dropped.\n\n";

        // Error message displayed when region is not provided.
        private const string NoRegionMessage = "\n\nNo region is configured, please make sure one is configured; e.g: 'eu', 'us'.\n\n";

        // Newline char to trim from message for formatting.
        private static readonly char[] _trimChars = { '\r', '\n' };

        /** Linux new-line */
        private const char NixNewLine = '\n';

        /** Unicode line separator character */
        internal const string LineSeparator = "\u2028";

        // Restricted symbols that should not appear in host name.
        // See http://support.microsoft.com/kb/228275/en-us for details.
        private static readonly Regex _forbiddenHostNameChars = new Regex(@"[/\\\[\]\""\:\;\|\<\>\+\=\,\?\* _]{1,}", RegexOptions.Compiled);

        // UTF-8 output character set.
        private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false,true);

        // Tracks all active queues; ConcurrentDictionary used as a set so entries can be removed on dispose.
        private static readonly ConcurrentDictionary<BlockingCollection<string>, byte> _allQueues = new();

        /// <summary>
        /// Determines if the queue is empty after waiting the specified waitTime.
        /// Returns true or false if the underlying queues are empty.
        /// </summary>
        /// <param name="waitTime">The length of time the method should block before giving up waiting for it to empty.</param>
        /// <returns>True if the queue is empty, false if there are still items waiting to be written.</returns>
        public static bool AreAllQueuesEmpty(TimeSpan waitTime)
        {
            var start = DateTime.UtcNow;
            var then = DateTime.UtcNow;

            while (start.Add(waitTime) > then)
            {
                if (_allQueues.Keys.All(x => x.Count == 0))
                    return true;

                Thread.Sleep(100);
                then = DateTime.UtcNow;
            }

            return _allQueues.Keys.All(x => x.Count == 0);
        }

        public AsyncLogger()
        {
            _queue = new BlockingCollection<string>(QueueSize);
            _threadCancellationTokenSource = new CancellationTokenSource();
            _allQueues.TryAdd(_queue, 0);

            _workerThread = new Thread(Run);
        }

        private string _logToken = "";
        private bool _debugEnabled = false;
        private bool _useTls = false;

        // Properties for defining location of DataHub instance if one is used.
        private bool _useDataHub = false; // By default, R7Insight service is used instead of DataHub instance.
        private string _dataHubAddr = "";
        private int _dataHubPort = 0;

        // Properties to define host name of user's machine and define user-specified log ID.
        private bool _useHostName = false; // Defines whether to prefix log message with HostName or not.
        private string _hostName = ""; // User-defined or auto-defined host name (if not set in config. file)
        private string _logId = ""; // User-defined log ID to be prefixed to the log message.

        private string _logRegion = ""; // Mandatory region option, e.g: us, eu

        // Sets DataHub usage flag.
        public void SetIsUsingDataHub(bool useDataHub)
        {
            _useDataHub = useDataHub;
        }

        // Sets DataHub instance address.
        public void SetDataHubAddr(string dataHubAddr)
        {
            _dataHubAddr = dataHubAddr;
        }

        // Sets the port on which DataHub instance is waiting for log messages.
        public void SetDataHubPort(int port)
        {
            _dataHubPort = port;
        }

        public void SetToken(string token)
        {
            _logToken = token;
        }

        public void SetDebug(bool debug)
        {
            _debugEnabled = debug;
        }

        public void SetUseSsl(bool useTls)
        {
            _useTls = useTls;
        }

        public void SetUseHostName(bool useHostName)
        {
            _useHostName = useHostName;
        }

        public void SetHostName(string hostName)
        {
            _hostName = hostName;
        }

        public void SetLogId(string logId)
        {
            _logId = logId;
        }

        public void SetRegion(string region)
        {
            _logRegion = region;
        }

        private readonly BlockingCollection<string> _queue;
        private Thread _workerThread;
        private CancellationTokenSource _threadCancellationTokenSource;
        private readonly Random _random = new Random();

        private InsightTcpClient? _insightTcpClient;
        private volatile bool _isRunning;
        private readonly object _startLock = new();

        private string _logMessagePrefix = string.Empty;

        private void Run()
        {
            ReopenConnection();

            if (_useHostName) ConfigureHostName();
            if (_logId != string.Empty) _logMessagePrefix = _logId + " ";
            if (_useHostName) _logMessagePrefix += _hostName;
            var isPrefixEmpty = _logMessagePrefix == string.Empty;

            var cancellationToken = _threadCancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    ProcessQueueItem(isPrefixEmpty, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_debugEnabled) WriteDebugMessages("Worker error, reopening connection.", ex);
                    ReopenConnection();
                }
            }

            CloseConnection();
        }

        private void ProcessQueueItem(bool isPrefixEmpty, CancellationToken cancellationToken)
        {
            if (_debugEnabled) WriteDebugMessages("Await queue data");

            var logLine = StringBuilderCache.Acquire();

            var line = _queue.Take(cancellationToken);
            if (_debugEnabled) WriteDebugMessages("Queue data obtained");

            if (!_useDataHub) logLine.Append(_logToken);
            if (!isPrefixEmpty) logLine.Append(_logMessagePrefix);

            // Replace newlines inline — avoids the intermediate string that ReplaceLineEndings() would allocate.
            AppendWithNewlineReplacement(logLine, line);
            logLine.Append(NixNewLine);

            var text = StringBuilderCache.GetStringAndRelease(logLine);
            var byteCount = _utf8.GetByteCount(text);
            var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                var written = _utf8.GetBytes(text, buffer);
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_debugEnabled) WriteDebugMessages("Write data");
                        _insightTcpClient!.Write(buffer.AsSpan(0, written));
                        if (_debugEnabled) WriteDebugMessages("Write complete");
                    }
                    catch (IOException e)
                    {
                        if (_debugEnabled) WriteDebugMessages("IOException during write, reopen: ", e);
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        ReopenConnection();
                        continue;
                    }

                    break;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        internal static void AppendWithNewlineReplacement(StringBuilder sb, string source)
        {
            var span = source.AsSpan();
            int start = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] is '\r' or '\n')
                {
                    if (start < i) sb.Append(span[start..i]);
                    sb.Append(LineSeparator);
                    if (span[i] == '\r' && i + 1 < span.Length && span[i + 1] == '\n')
                        i++;
                    start = i + 1;
                }
            }
            if (start < span.Length) sb.Append(span[start..]);
        }

        private void ConfigureHostName()
        {
            // If LogHostName is set to “true”, but HostName is not defined -
            // try to get host name from Environment.
            if (string.IsNullOrEmpty(_hostName))
            {
                try
                {
                    if (_debugEnabled) WriteDebugMessages("HostName parameter is not defined - trying to get it from System.Environment.MachineName");

                    var hostName = Environment.MachineName;
                    _hostName = "HostName=" + hostName + " ";
                }
                catch (Exception ex)
                {
                    // Cannot get host name automatically, so assume that HostName is not used
                    // and log message is sent without it.
                    _useHostName = false;
                    if (_debugEnabled) WriteDebugMessages("Failed to get HostName parameter using System.Environment.MachineName. Log messages will not be prefixed by HostName", ex);
                }
                return;
            }

            if (!CheckIfHostNameValid(_hostName))
            {
                // If user-defined host name is incorrect - we cannot use it
                // and log message is sent without it.
                _useHostName = false;
                if (_debugEnabled) WriteDebugMessages("HostName parameter contains prohibited characters. Log messages will not be prefixed by HostName");
            }
            else
            {
                _hostName = "HostName=" + _hostName + " ";
            }
        }

        private void OpenConnection(CancellationToken cancellationToken)
        {
            try
            {
                if (_insightTcpClient == null)
                {
                    _insightTcpClient = new InsightTcpClient(_useTls, _useDataHub, _dataHubAddr, _dataHubPort, _logRegion);
                }

                _insightTcpClient.Connect(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new IOException("An error occurred while opening the connection.", ex);
            }
        }

        private void ReopenConnection()
        {
            if (_debugEnabled) WriteDebugMessages("ReopenConnection");
            CloseConnection();

            var cancellationToken = _threadCancellationTokenSource.Token;

            var rootDelay = MinDelay;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    OpenConnection(cancellationToken);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    WriteDebugMessages($"Unable to connect to Rapid7 Insight API at {(_insightTcpClient != null ? _insightTcpClient.ServerAddr : "null")}:{_insightTcpClient?.TcpPort ?? 0}", ex);
                }

                rootDelay *= 2;
                if (rootDelay > MaxDelay)
                    rootDelay = MaxDelay;

                var waitFor = rootDelay + _random.Next(rootDelay);
                WriteDebugMessages($"Waiting {waitFor} ms for retry");

                cancellationToken.WaitHandle.WaitOne(waitFor);
            }
        }

        private void CloseConnection()
        {
            _insightTcpClient?.Close();
        }

        private bool IsConfigured()
        {
            if (string.IsNullOrEmpty(_logRegion))
            {
                WriteDebugMessages(NoRegionMessage);
                return false;
            }
            if (GetIsValidGuid(_logToken))
                return true;

            WriteDebugMessages(InvalidTokenMessage);
            return false;
        }

        internal static bool CheckIfHostNameValid(string hostName)
        {
            return !_forbiddenHostNameChars.IsMatch(hostName); // Returns false if reg.ex. matches any of forbidden chars.
        }

        private static bool GetIsValidGuid(string guidString)
        {
            if (string.IsNullOrEmpty(guidString))
                return false;

            return Guid.TryParse(guidString, out var newGuid) && newGuid != Guid.Empty;
        }

        private void WriteDebugMessages(string message, Exception ex)
        {
            if (!_debugEnabled)
                return;

            SelfLog.WriteLine(InternalLogPrefix, string.Concat(message, ex.ToString()));
        }

        private void WriteDebugMessages(string message)
        {
            if (!_debugEnabled)
                return;

            SelfLog.WriteLine(InternalLogPrefix, message);
        }

        private void WriteDebugMessagesFormat<T>(string message, T arg0)
        {
            if (!_debugEnabled)
                return;

            WriteDebugMessages(string.Format(message, arg0));
        }

        public void QueueLogEvent(string line)
        {
            QueueLogEntry(line, RecursionLimit);
        }

        private void QueueLogEntry(string line, int limit)
        {
            while (true)
            {
                if (limit == 0)
                {
                    if (_debugEnabled) WriteDebugMessagesFormat("Message longer than {0}", RecursionLimit * LogLengthLimit);
                    return;
                }

                if (_debugEnabled) WriteDebugMessagesFormat("Adding Line: {0}", line);
                if (!_isRunning)
                {
                    // Emit() is called concurrently from multiple threads in a real host (unlike a
                    // single-threaded console test), so the lazy start must be atomic: without this
                    // lock, two racing threads can both see _isRunning == false and both call
                    // Start() on the same Thread, throwing and silently killing logging (Serilog
                    // swallows sink exceptions via SelfLog).
                    lock (_startLock)
                    {
                        if (!_isRunning)
                        {
                            // If in DataHub mode credentials are ignored.
                            if (!_useDataHub && IsConfigured() || _useDataHub)
                            {
                                if (_debugEnabled) WriteDebugMessages("Starting Rapid7 Insight asynchronous socket client.");
                                _workerThread.Name = "Rapid7InsightOpsLogAppender";
                                _workerThread.IsBackground = true;
                                _workerThread.Start();
                                _isRunning = true;
                            }
                        }
                    }
                }

                if (_debugEnabled) WriteDebugMessagesFormat("Queueing: {0}", line);

                var chunkedEvent = line.TrimEnd(_trimChars);
                if (chunkedEvent.Length > LogLengthLimit)
                {
                    AddChunkToQueue(chunkedEvent.Substring(0, LogLengthLimit));
                    line = chunkedEvent.Substring(LogLengthLimit);
                    limit -= 1;
                    continue;
                }

                AddChunkToQueue(chunkedEvent);

                break;
            }
        }


        public void InterruptWorker()
        {
            if (!_isRunning)
            {
                CloseConnection();
                return;
            }

            try
            {
                _threadCancellationTokenSource.Cancel();
                _workerThread.Join(1000);
            }
            finally
            {
                _allQueues.TryRemove(_queue, out _);
                CloseConnection();
                _threadCancellationTokenSource = new CancellationTokenSource();
                _workerThread = new Thread(Run);
                _isRunning = false;
            }
        }

        public bool FlushQueue(TimeSpan waitTime)
        {
            var cancellationToken = _threadCancellationTokenSource.Token;

            var startTime = DateTime.UtcNow;
            while (_queue.Count != 0)
            {
                if (!_isRunning)
                    break;

                if (cancellationToken.IsCancellationRequested)
                    break;

                cancellationToken.WaitHandle.WaitOne(100);
                if (DateTime.UtcNow - startTime > waitTime)
                    break;
            }
            return _queue.Count == 0;
        }

        private void AddChunkToQueue(string chunkedEvent)
        {
            // Try to append data to queue.
            if (_queue.TryAdd(chunkedEvent)) return;

            // If queue is full, remove the oldest message and try again.
            WriteDebugMessages(QueueOverflowMessage);
            _queue.Take();
            _queue.TryAdd(chunkedEvent);
        }
    }
}
