using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Serilog.Sinks.InsightIDR.Rapid7
{
    internal sealed class InsightTcpClient
    {
        private const string DataUrl = "{0}.data.logs.insight.rapid7.com";
        private const int UnsecurePort = 80;
        private const int SecurePort = 443;

        public InsightTcpClient(bool useSsl, bool useDataHub, string serverAddr, int port, string region)
        {
            if (useDataHub)
            {
                _useTls = false; // DataHub does not support receiving log messages over SSL for now.
                TcpPort = port;
                ServerAddr = serverAddr;
                return;
            }

            _useTls = useSsl;
            TcpPort = _useTls ? SecurePort : UnsecurePort;
            ServerAddr = string.Format(DataUrl, region);
        }

        private readonly bool _useTls;
        public int TcpPort { get; }
        private TcpClient? _tcpClient;
        private Stream? _stream;
        private SslStream? _tlsStream;
        public string ServerAddr { get; } = "";

        private Stream ActiveStream => _useTls ? _tlsStream! : _stream!;

        private static void SetSocketKeepAliveValues(TcpClient tcpClient, int keepAliveTime, int keepAliveInterval)
        {
            if (OperatingSystem.IsWindows())
            {
                const uint dummy = 0;
                var inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];
                const bool onOff = true;

                BitConverter.GetBytes((uint)(onOff ? 1 : 0)).CopyTo(inOptionValues, 0);
                BitConverter.GetBytes((uint)keepAliveTime).CopyTo(inOptionValues, Marshal.SizeOf(dummy));
                BitConverter.GetBytes((uint)keepAliveInterval).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);

                tcpClient.Client.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
                return;
            }

            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.TcpKeepAliveTime, keepAliveTime);
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.TcpKeepAliveInterval, keepAliveInterval);
        }

        public void Connect(CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            _tcpClient = new TcpClient();
            _tcpClient.ConnectAsync(ServerAddr, TcpPort, cts.Token).GetAwaiter().GetResult();
            _tcpClient.NoDelay = true;

            _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            try
            {
                SetSocketKeepAliveValues(_tcpClient, 10 * 1000, 1000);
            }
            catch (PlatformNotSupportedException)
            {
                // .NET on Linux does not support modification of that setting at the moment. Defaults applied.
                // ignore
            }

            _stream = _tcpClient.GetStream();

            if (!_useTls) return;

            _tlsStream = new SslStream(_stream);
            _tlsStream.AuthenticateAsClientAsync(new System.Net.Security.SslClientAuthenticationOptions { TargetHost = ServerAddr }, cts.Token).GetAwaiter().GetResult();
        }

        public void Write(ReadOnlySpan<byte> buffer)
        {
            ActiveStream.Write(buffer);
            ActiveStream.Flush();
        }

        public void Close()
        {
            if (_tcpClient == null) return;

            try
            {
                _tlsStream?.Dispose();
                _stream?.Dispose();
                _tcpClient.Dispose();
            }
            catch
            {
                // ignored
            }
            finally
            {
                _tcpClient = null;
                _stream = null;
                _tlsStream = null;
            }
        }
    }
}
