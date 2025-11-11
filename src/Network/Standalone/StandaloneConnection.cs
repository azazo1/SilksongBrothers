using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MemoryPack;

namespace SilksongBrothers.Network.Standalone;

public class StandaloneConnection : IConnection
{
    private volatile TcpClient _client = new();
    private readonly Throttler _realtimeDebugThrottler = new(1000);
    private CancellationTokenSource _connectionCts = new();

    /// <summary>
    /// PacketType => handlers callback
    /// </summary>
    private readonly Dictionary<Type, Action<Packet>> _handlers = new();

    public bool Connected => _client.Connected;
    public Action OnConnected { get; set; } = () => { };
    public Action<Exception> OnConnectFailed { get; set; } = _ => { };
    public Action<Exception> OnConnectionCrashed { get; set; } = _ => { };

    /// <summary>
    /// 接收线程放入 packet, 在 <see cref="Update"/> 获取.
    /// </summary>
    private readonly Queue<Packet> _rxQueue = new();

    private readonly BlockingCollection<Packet> _txQueue = new();

    public void Establish()
    {
        if (Connected)
        {
            return;
        }

        _connectionCts = new CancellationTokenSource();

        var parts = ModConfig.StandaloneServerAddress.Split(":", StringSplitOptions.RemoveEmptyEntries);
        var hostname = parts[0];
        var port = int.Parse(parts[1]);
        try
        {
            _client.Connect(hostname, port);
        }
        catch (SocketException e)
        {
            OnConnectFailed.Invoke(e);
            return;
        }

        OnConnected.Invoke();

        Task.Factory.StartNew(
            async () =>
            {
                try
                {
                    await Task.WhenAll(RxTask(), TxTask());
                }
                catch (Exception e)
                {
                    OnConnectionCrashed.Invoke(e);
                }
            },
            _connectionCts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    public void Destroy()
    {
        _connectionCts.Cancel();
        _client.Close();
    }

    public void Send<T>(T packet) where T : Packet
    {
        _txQueue.Add(packet);
    }

    public Action<Packet> AddHandler<T>(Action<T> handler) where T : Packet
    {
        // 不能直接放置 Action<T> 需要在 lambda 内转换一下, 由于创建的是 lambda 函数,
        // 在删除的时候需要使用这个 lambda 来删除, 因此提供一个返回值, 用于 RemoveHandler.
        Action<Packet> h = packet => handler((T)packet);
        if (!_handlers.ContainsKey(typeof(T)))
        {
            _handlers.Add(typeof(T), h);
        }
        else
        {
            _handlers[typeof(T)] += h;
        }

        Utils.Logger?.LogDebug($"Client connection handler of {typeof(T).Name} added.");
        return h;
    }

    public void RemoveHandler<T>(Action<Packet> handler) where T : Packet
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers)) return;
        handlers -= handler;
        if (handlers == null)
        {
            _handlers.Remove(typeof(T));
        }
        else
        {
            _handlers[typeof(T)] = handlers;
        }
    }

    public void RemoveHandlersOfType<T>() where T : Packet
    {
        _handlers.Remove(typeof(T));
    }

    public void RemoveHandlers()
    {
        _handlers.Clear();
    }

    private async Task RxTask()
    {
        var stream = _client.GetStream();
        while (Connected && !_connectionCts.IsCancellationRequested)
        {
            var packet = await stream.ReceivePacketAsync(_connectionCts.Token);
            if (packet == null) continue;
            Utils.Logger?.LogDebug($"Client received packet {packet.GetType().Name}.");
            _rxQueue.Enqueue(packet);
        }
    }

    private async Task TxTask()
    {
        var stream = _client.GetStream();
        while (Connected && !_connectionCts.IsCancellationRequested)
        {
            var packet = _txQueue.Take();
            await stream.SendPacketAsync(packet, _connectionCts.Token);
            Utils.Logger?.LogDebug($"Client sent packet {packet.GetType().Name}.");
        }
    }

    public void Update()
    {
        var startTime = Utils.Time;
        while (Utils.Time - startTime < Constants.ConnectionUpdateMaxDuration
               && _rxQueue.TryDequeue(out var packet))
        {
            // 发送目标不是自己.
            if (packet.DstPeer != null && !packet.DstPeer.Contains(ModConfig.StandalonePeerId))
            {
                continue;
            }

            // 过滤超时的实时包.
            var curTime = Utils.ServerTime;
            if (packet.IsRealtime && curTime - packet.Time > ModConfig.RealtimeTimeout)
            {
                if (_realtimeDebugThrottler.Tick())
                {
                    Utils.Logger?.LogDebug("Client dropped outdated realtime packet.");
                }

                continue;
            }

            // 获取 handlers
            if (!_handlers.TryGetValue(packet.GetType(), out var handlers)
                || handlers == null)
                continue;
            handlers.Invoke(packet);
        }
    }
}
