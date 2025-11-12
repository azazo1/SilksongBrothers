using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SilksongBrothers.Network.Standalone;

// todo 连接总是莫名其妙关闭但是客户端没察觉到.

internal class Peer(string id, TcpClient client)
{
    public string Id => id;
    public TcpClient Client { get; set; } = client;
    public string? Scene { get; set; }
}

internal class PeerRegistry
{
    private readonly Dictionary<string, Peer> _peers = new();
    private string? _host;

    public string? Host
    {
        get => _host;

        private set
        {
            var oldHost = _host;
            _host = value;
            Utils.Logger?.LogInfo($"Server host peer changed to {_host}.");
            if (oldHost != value)
                OnHostChanged.Invoke((oldHost, _host));
        }
    }

    private readonly Dictionary<TcpClient, string> _peersRev = new();
    public Dictionary<string, Peer>.KeyCollection PeerIds => _peers.Keys;
    public Dictionary<TcpClient, string>.KeyCollection Clients => _peersRev.Keys;

    /// <summary>
    /// (旧 Host, 新 Host), 保证调用的时候会有变化.
    /// </summary>
    /// <returns></returns>
    public Action<(string?, string?)> OnHostChanged { get; set; } = _ => { };


    /// <summary>
    /// 向记录中添加新的 Peer 或者更新现有的 Peer 连接.
    /// </summary>
    public void Update(string id, TcpClient client)
    {
        if (_peers.IsNullOrEmpty())
        {
            Host = id;
        }

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
        if (id == Host) RandomChoiceHost();
    }

    public void RandomChoiceHost()
    {
        if (_peers.IsNullOrEmpty())
        {
            Host = null;
            return;
        }

        var idx = RandomNumberGenerator.GetInt32(PeerIds.Count);
        Host = PeerIds.ElementAt(idx);
    }
}

/// <summary>
/// 方法中的 io 操作都使用异步运行时运行, 不会直接在调用的时候执行, 可以通过 <see cref="Action{T}"/> 来回调获取结果.
/// </summary>
///
/// 在服务端创建 Packet 需要注意调用 CreatePacket 方法, 能够统一调整 SrcPeer 为 <see cref="Constants.ServerId"/>
public class StandaloneServer
{
    // Create 这个操作应该是不产生 io 操作的.
    private volatile TcpListener _listener = TcpListener.Create(ModConfig.StandaloneServerPort);
    public bool Running => !_cts.IsCancellationRequested;
    public Action<Exception> OnServerCrashed { get; set; } = _ => { };
    private volatile PeerRegistry _peers = new();
    private volatile CancellationTokenSource _cts = new();
    private readonly ConcurrentBag<Task> _taskBag = [];

    public StandaloneServer()
    {
        _cts.Cancel(); // 初始是非 Running 状态.
    }

    private delegate void PacketInitializer<T>(ref T packet) where T : Packet;

    private static T CreatePacket<T>(PacketInitializer<T>? initialize = null) where T : Packet
    {
        var packet = Activator.CreateInstance<T>();
        packet.SrcPeer = Constants.ServerId;
        initialize?.Invoke(ref packet);
        return packet;
    }

    /// <summary>
    /// 启动服务器, 服务器在新的线程中执行循环.
    /// </summary>
    public void Start()
    {
        if (Running) return;
        _listener = TcpListener.Create(ModConfig.StandaloneServerPort);
        _peers = new PeerRegistry();
        _peers.OnHostChanged += OnHostChanged;
        _cts = new CancellationTokenSource();
        _taskBag.Add(Task.Run(async Task () =>
        {
            try
            {
                _listener.Start();
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
        _listener.Stop();
        foreach (var client in _peers.Clients)
        {
            client.Close();
        }

        await Task.WhenAll(_taskBag);
        _taskBag.Clear();
    }

    private void OnHostChanged((string?, string?) changes)
    {
        _ = SendPacket(CreatePacket<HostPeerPacket>((ref p) => { p.Host = changes.Item2; }));
    }

    private async Task ServerLoop()
    {
        try
        {
            var acceptTask = _listener.AcceptTcpClientAsync();
            var hostChangeTask = Task.Delay(ModConfig.ServerHostChangeInterval);
            while (Running)
            {
                var task = await Task.WhenAny(
                    acceptTask,
                    hostChangeTask
                );
                if (task == acceptTask)
                {
                    _taskBag.Add(Serve(await acceptTask)); // 后台服务客户端.
                    acceptTask = _listener.AcceptTcpClientAsync();
                }
                else if (task == hostChangeTask)
                {
                    hostChangeTask = Task.Delay(ModConfig.ServerHostChangeInterval);
                    _peers.RandomChoiceHost();
                }
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
        var clientRemoteEndPoint = client.Client.RemoteEndPoint;
        string? peerId = null;
        Utils.Logger?.LogDebug($"Server peer connection established: {clientRemoteEndPoint}.");
        try
        {
            var receivePacketTask = client.GetStream().ReceivePacketAsync(_cts.Token);
            while (Running && client.Connected)
            {
                var task = await Task.WhenAny(receivePacketTask, Task.Delay(3000));
                if (task == receivePacketTask)
                {
                    var packet = await receivePacketTask;
                    receivePacketTask = client.GetStream().ReceivePacketAsync(_cts.Token);
                    if (packet == null) continue;
                    if (packet.SrcPeer == Constants.ServerId) continue; // 简单地防止假装.
                    peerId ??= packet.SrcPeer;
                    // todo 恢复 logging
                    // Utils.Logger?.LogDebug($"Server received packet {packet.GetType().Name}.");
                    if (!packet.IsRealtime)
                    {
                        Utils.Logger?.LogDebug($"Server received packet: {packet.GetType().Name}");
                    }

                    await HandlePacket(packet, client);
                }
                else
                {
                    // 接收超时则发送心跳包.
                    await SendPacketToClient(CreatePacket<HeartbeatPacket>(), client);
                }
            }

            Utils.Logger?.LogDebug(
                $"Server peer connection `{clientRemoteEndPoint}`({peerId}) closed.");
        }
        catch (Exception e)
        {
            // 不知道为什么这里的 TrimEnd 还是无法去除末尾的空白字符.
            Utils.Logger?.LogDebug(
                $"Server peer connection `{clientRemoteEndPoint}`({peerId}) exception occurred: {e.Message.TrimEnd()}");
        }
        finally
        {
            client.Close();
            if (peerId != null)
            {
                await SendPacket(CreatePacket<PeerQuitPacket>((ref p) => { p.QuitPeer = peerId; }));
                _peers.Remove(peerId);
            }
        }
    }

    private async Task HandlePacket(Packet packet, TcpClient client)
    {
        if (packet.IsRealtime && Utils.ServerTime - packet.Time > ModConfig.RealtimeTimeout) return;
        switch (packet)
        {
            case PeerIdPacket peerIdPacket:
                _peers.Update(peerIdPacket.SrcPeer, client);
                await SendPacket(peerIdPacket);
                break;
            case SyncTimePacket:
                await SendPacketToClient(CreatePacket<SyncTimePacket>(), client);
                break;
            case PeerQuitPacket: // 仅服务端可发送.
            case HeartbeatPacket: // 无需响应.
                break;
            case HostPeerPacket:
                await SendPacketToClient(
                    CreatePacket<HostPeerPacket>((ref p) => { p.Host = _peers.Host; }),
                    client);
                break;
            default:
                await SendPacket(packet);
                break;
        }
    }


    private async Task SendPacket(Packet packet)
    {
        List<Task> sendHandles = [];
        var dstPeer = packet.DstPeer;
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
            foreach (var peerId in _peers.PeerIds)
            {
                if (peerId == packet.SrcPeer) continue;
                var peer = _peers.Query(peerId);
                sendHandles.Add(SendPacketToClient(packet, peer!.Client));
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
        // todo 恢复 logging
        // Utils.Logger?.LogDebug($"Server sent packet {packet.GetType().Name}.");
        if (!packet.IsRealtime)
        {
            Utils.Logger?.LogDebug($"Server sent packet {packet.GetType().Name}.");
        }

        await stream.SendPacketAsync(packet, _cts.Token);
    }
}
