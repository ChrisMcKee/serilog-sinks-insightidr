namespace Serilog.Sinks.InsightIDR.Rapid7
{
    /// <summary>
    /// A single outbound connection to a Rapid7 InsightIDR (or DataHub) endpoint.
    /// Abstracted from <see cref="InsightTcpClient"/> so tests can substitute a fake transport
    /// without opening real sockets.
    /// </summary>
    internal interface IInsightConnection : IDisposable
    {
        /// <summary>Connects if not already connected; otherwise a no-op.</summary>
        Task EnsureConnectedAsync(CancellationToken cancellationToken = default);

        Task WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
    }
}
