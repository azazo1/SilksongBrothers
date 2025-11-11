using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MemoryPack;

namespace SilksongBrothers.Network.Standalone;

internal class Peer(string id, TcpClient client)
{
    public string Id => id;
    public TcpClient Client { get; set; } = client;
    public string? Scene { get; set; }
}

internal class PeerRegistry
{
    private readonly Dictionary<string, Peer> _peers = new();
    private readonly Dictionary<TcpClient, string> _peersRev = new();
    public Dictionary<string, Peer>.KeyCollection PeerIds => _peers.Keys;
    public Dictionary<TcpClient, string>.KeyCollection Clients => _peersRev.Keys;


    /// <summary>
    /// 向记录中添加新的 Peer 或者更新现有的 Peer 连接.
    /// </summary>
    public void Update(string id, TcpClient client)
    {
        if (_peers.TryGetValue(id, out var peer))
        {
            if (peer.Client == client) return;
            peer.Client.Close();
            _peersRev.Remove(peer.Client);
            peer.Client = client;
            _peersRev.Add(client, id);
        }
        else
        {
            _peers.Add(id, new Peer(id, client));
            _peersRev.Add(client, id);
        }
    }

    /// <summary>
    /// 使用 client 获取 peer id
    /// </summary>
    public string? Query(TcpClient client)
    {
        _peersRev.TryGetValue(client, out var peerId);
        return peerId;
    }

    /// <summary>
    /// 使用 peer id 获取 peer.
    /// </summary>
    public Peer? Query(string id)
    {
        _peers.TryGetValue(id, out var peer);
        return peer;
    }

    /// <summary>
    /// 删除对应 peer id 的 peer.
    /// </summary>
    public void Remove(string id)
    {
        if (!_peers.TryGetValue(id, out var peer)) return;
        _peersRev.Remove(peer.Client);
        _peers.Remove(id);
    }
}

/// <summary>
/// 方法中的 io 操作都使用异步运行时运行, 不会直接在调用的时候执行, 可以通过 <see cref="Action{T}"/> 来回调获取结果.
/// </summary>
public class StandaloneServer
{
    // Create 这个操作应该是不产生 io 操作的.
    private volatile TcpListener _listener = TcpListener.Create(ModConfig.StandaloneServerPort);
    public bool Running { get; private set; }
    public Action<Exception> OnServerCrashed { get; set; } = _ => { };
    private volatile PeerRegistry _peers = new();
    private volatile CancellationTokenSource _cts = new();
    private readonly ConcurrentBag<Task> _taskBag = [];

    /// <summary>
    /// 启动服务器, 服务器在新的线程中执行循环.
    /// </summary>
    public void Start()
    {
        if (Running) return;
        _listener = TcpListener.Create(ModConfig.StandaloneServerPort);
        _peers = new PeerRegistry();
        _cts = new CancellationTokenSource();
        _taskBag.Add(Task.Run(async Task () =>
        {
            try
            {
                _listener.Start();
                Running = true;
                await ServerLoop();
            }
            catch (Exception e)
            {
                OnServerCrashed.Invoke(e);
            }
        }, _cts.Token));
    }

    /// <summary>
    /// 关闭服务器, 此方法会通知然后等待所有的已创建连接结束, 再关闭服务器.
    /// </summary>
    public async Task Stop()
    {
        _cts.Cancel();
        Running = false;
        _listener.Stop();
        foreach (var client in _peers.Clients)
        {
            client.Close();
        }

        await Task.WhenAll(_taskBag);
        _taskBag.Clear();
    }

    private async Task ServerLoop()
    {
        try
        {
            while (Running)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _taskBag.Add(Serve(client)); // 后台服务客户端.
            }
        }
        catch (ObjectDisposedException)
        {
            // ignore
            // 在关闭服务器的时候会触发这个错误, 不知道怎么关闭.
        }
    }

    private async Task Serve(TcpClient client)
    {
        Utils.Logger?.LogDebug($"Server peer connection established: {client.Client.RemoteEndPoint}.");
        try
        {
            while (Running && client.Connected)
            {
                var packet = await client.GetStream().ReceivePacketAsync(_cts.Token);
                if (packet == null) continue;
                Utils.Logger?.LogDebug($"Server received packet {packet.GetType().Name}.");
                _taskBag.Add(HandlePacket(packet, client));
            }
        }
        catch (Exception e)
        {
            Utils.Logger?.LogDebug($"Server peer connection {client.Client.RemoteEndPoint} closed: {e.Message}");
        }
    }

    private async Task HandlePacket(Packet packet, TcpClient client)
    {
        if (packet.IsRealtime && Utils.ServerTime - packet.Time > ModConfig.RealtimeTimeout) return;
        var peerId = _peers.Query(client);
        // 自动设置 SrcPeer.
        if (packet.SrcPeer == null && peerId != null)
        {
            packet.SrcPeer = peerId;
        }

        switch (packet)
        {
            case PeerIdPacket peerIdPacket:
                if (peerIdPacket.SrcPeer != null)
                    _peers.Update(peerIdPacket.SrcPeer, client);
                break;
            case SyncTimePacket:
                await SendPacketToClient(new SyncTimePacket(), client);
                break;
            case PeerQuitPacket peerQuitPacket:
                if (peerQuitPacket.SrcPeer != null) // 无来源的直接丢弃.
                {
                    _peers.Remove(peerQuitPacket.SrcPeer);
                    await SendPacket(peerQuitPacket, null);
                }

                break;
        }
    }


    private async Task SendPacket(Packet packet, string[]? dstPeer)
    {
        if (packet.SrcPeer == null) // 无来源的 packet 选择直接抛弃.
        {
            Utils.Logger?.LogDebug("Sending packet with ");
            return;
        }

        List<Task> sendHandles = [];
        if (dstPeer != null)
        {
            foreach (var peerId in dstPeer)
            {
                if (peerId == packet.SrcPeer) continue;
                var peer = _peers.Query(peerId);
                if (peer == null) continue;
                sendHandles.Add(SendPacketToClient(packet, peer.Client));
            }
        }
        else
        {
            foreach (var client in _peers.Clients)
            {
                sendHandles.Add(SendPacketToClient(packet, client));
            }
        }

        foreach (var task in sendHandles)
        {
            await task;
        }
    }

    private async Task SendPacketToClient(Packet packet, TcpClient client)
    {
        if (!client.Connected) return;
        var stream = client.GetStream();
        Utils.Logger?.LogDebug($"Server received packet {packet.GetType().Name}.");
        await stream.SendPacketAsync(packet, _cts.Token);
    }
}
