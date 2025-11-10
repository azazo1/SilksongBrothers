using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MemoryPack;

namespace SilksongBrothers.Network;

public class StandaloneConnection : IConnection
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly Throttler _realtimeDebugThrottler = new(1000);

    /// <summary>
    /// PacketType(int) => handlers callback
    /// </summary>
    private readonly Dictionary<Type, Action<Packet>> _handlers = new();

    public bool Connected => _client?.Connected ?? false;

    /// <summary>
    /// 接收线程放入 packet, 在 <see cref="Update"/> 获取.
    /// </summary>
    private readonly Queue<Packet> _rxQueue = new();

    public void Establish()
    {
        var parts = ModConfig.StandaloneServerAddress.Split(":", StringSplitOptions.RemoveEmptyEntries);
        var hostname = parts[0];
        var port = int.Parse(parts[1]);
        _client = new TcpClient(hostname, port);
        _stream = _client.GetStream();

        Task.Factory.StartNew(
            RxTask,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    public void Destroy()
    {
        _client?.Close();
        _client = null;
        _stream?.Close();
        _stream = null;
    }

    public void Send<T>(T packet, string[]? dstPeers, bool realtime = false) where T : Packet
    {
        var stream = _stream;
        if (stream == null)
        {
            return;
        }

        var buf = MemoryPackSerializer.Serialize(packet);
        var lenBuf = buf.Length.ToLsbBytes();
        stream.Write(lenBuf, 0, lenBuf.Length);
        stream.Write(buf, 0, buf.Length);
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

        Utils.Logger?.LogDebug($"Connection handler of {typeof(T).Name} added.");
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
        var lenBuf = new byte[8];
        var read = 0;
        if (_stream != null)
        {
            _stream.ReadTimeout = 1000;
        }

        while (Connected)
        {
            var stream = _stream;
            if (stream == null)
            {
                Utils.Logger?.LogWarning("Client rx thread ended for stream is null.");
                return;
            }

            // 读取数据包长度.
            read += await stream.ReadAsync(lenBuf);
            while (read < 8)
            {
                read += await stream.ReadAsync(lenBuf.AsMemory(read, lenBuf.Length - read));
            }

            read = 0;
            // 读取数据包.
            var len = lenBuf.ToLsbInt();
            var pendingRead = len;
            if (len > Constants.MaxPacketLen)
            {
                Utils.Logger?.LogWarning(
                    "Client received packet length that is bigger than MaxPacketLen, which might be a hack packet.");
                // 暂时直接将包消耗, 忽略其内容.
                var buf = new byte[4096];
                while (pendingRead > 0)
                {
                    pendingRead -= await stream.ReadAsync(buf.AsMemory(0, Math.Min(buf.Length, pendingRead)));
                }

                continue;
            }

            // Utils.Logger?.LogDebug($"Client received packet length: {len}");
            var data = new byte[len];
            while (pendingRead > 0)
            {
                pendingRead -= await stream.ReadAsync(data.AsMemory(len - pendingRead, pendingRead));
            }

            // 解包
            var packet = MemoryPackSerializer.Deserialize<Packet>(data);
            if (packet != null)
            {
                _rxQueue.Enqueue(packet);
            }
            else
            {
                Utils.Logger?.LogError("Client rx thread received invalid bytes to deserialize packet");
            }
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
            var curTime = Utils.Time;
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
