using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Lexie.Services;

public class WsjtxUdpService : IDisposable
{
    private UdpClient? _client;
    private CancellationTokenSource? _cts;

    private readonly Channel<byte[]> _channel = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<byte[]> Messages => _channel.Reader;

    public async Task StartAsync(int port = 2237, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Create socket manually to set ReuseAddress BEFORE binding
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, port));

        _client = new UdpClient { Client = socket };

        // Try multicast join (non-fatal)
        try
        {
            _client.JoinMulticastGroup(IPAddress.Parse("239.255.0.0"));
        }
        catch
        {
            // Not needed for local WSJT-X unicast
        }

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var result = await _client.ReceiveAsync(_cts.Token);
                await _channel.Writer.WriteAsync(result.Buffer, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            _channel.Writer.Complete();
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        _client?.Dispose();
        _cts?.Dispose();
    }
}
