using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using MemoryPack;

namespace SilksongBrothers;

public static class Utils
{
    private static Version? _version;

    /// <summary>
    /// 和服务器校准的时间差值.
    /// </summary>
    private static long _timeOffset;

    [SuppressMessage("Usage", "CA2211:非常量字段应当不可见")]
    public static ManualLogSource? Logger;

    public static Version Version => _version ??= Assembly.GetExecutingAssembly().GetName().Version;

    public static string GeneratePeerId() => Guid.NewGuid().ToString();

    public static void SetTimeOffset(long offset) => _timeOffset = offset;

    /// <summary>
    /// 和服务器校准过后的当前时间.
    /// </summary>
    public static long ServerTime => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _timeOffset;

    public static long Time => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static byte[] ToLsbBytes(this int val)
    {
        var buf = BitConverter.GetBytes(val);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(buf);
        }

        return buf;
    }

    public static int ToLsbInt(this byte[] lsbBuf)
    {
        if (lsbBuf.Length != 4)
        {
            throw new ArgumentException("ToLsbInt: lsbBuf must be 4 bytes");
        }

        var lsbBufCopy = new byte[4];
        Array.Copy(lsbBuf, lsbBufCopy, 4);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(lsbBufCopy);
        }

        return BitConverter.ToInt32(lsbBufCopy);
    }
}

public class Throttler(long interval)
{
    private long _lastActivatedTime;

    public bool Tick()
    {
        var curTime = Utils.Time;
        if (curTime - _lastActivatedTime < interval) return false;
        _lastActivatedTime = curTime;
        return true;
    }
}

public static class NetworkStreamExt
{
    public static async Task SendPacketAsync(this NetworkStream stream, Packet packet, CancellationToken token)
    {
        var data = MemoryPackSerializer.Serialize(packet);
        await stream.WriteAsync(data.Length.ToLsbBytes(), token);
        await stream.WriteAsync(data, token);
        await stream.FlushAsync(token);
    }

    /// <summary>
    /// 从流中读取一个 <see cref="Packet"/>
    /// </summary>
    /// <returns>尝试返回一个 packet, 如果连接已关闭, 或者对面发送了一个过长的 packet, 返回 null.</returns>
    /// <exception cref="SocketException">套接字错误</exception>
    public static async Task<Packet?> ReceivePacketAsync(this NetworkStream stream, CancellationToken token)
    {
        var lenBuf = await stream.ReadExactAsync(4, token);
        var len = lenBuf.ToLsbInt();
        if (len > Constants.MaxPacketLen)
        {
            await stream.ConsumeExactAsync(len, token);
            return null;
        }

        var data = await stream.ReadExactAsync(len, token);
        var packet = MemoryPackSerializer.Deserialize<Packet>(data);
        return packet;
        // 以下方法直接读取 packet 会有个问题: 只有在对面连接关闭的时候才会返回获取的 packet.
        /* var packet = await MemoryPackSerializer.DeserializeAsync<Packet>(
            stream,
            MemoryPackSerializerOptions.Default,
            token
        );
        return packet; */
    }

    public static async Task<byte[]> ReadExactAsync(this NetworkStream stream, int length, CancellationToken token)
    {
        var buf = new byte[length];
        var read = await stream.ReadAsync(buf, token);
        while (read < length)
        {
            read += await stream.ReadAsync(buf.AsMemory(read, length - read), token);
        }

        return buf;
    }

    public static async Task ConsumeExactAsync(
        this NetworkStream stream,
        int length,
        CancellationToken token,
        int bufLength = 1024
    )
    {
        var buf = new byte[bufLength];
        var read = 0;
        while (read < length)
        {
            read += await stream.ReadAsync(buf.AsMemory(0, Math.Min(length - read, bufLength)), token);
        }
    }
}

public class PacketLogger
{
    private readonly ManualLogSource? _logger;
    private readonly bool _throttleNonRealtime;
    private readonly Dictionary<Type, Throttler> _throttlers = new();
    private readonly Throttler _dummyThrottler = new Throttler(0);
    private readonly bool _isClient;
    private readonly bool _isSend;

    public PacketLogger(ManualLogSource? source, bool isClient, bool isSend, bool throttleNonRealtime = false)
    {
        _logger = source;
        _isClient = isClient;
        _isSend = isSend;
        _throttleNonRealtime = throttleNonRealtime;
    }

    public void Log<T>(LogLevel level, T packet) where T : Packet
    {
        var throttler = _dummyThrottler;
        if (packet.IsRealtime || _throttleNonRealtime)
            if (!_throttlers.TryGetValue(typeof(T), out throttler))
            {
                _throttlers.Add(typeof(T), new Throttler(1000));
                throttler = _throttlers[typeof(T)];
            }

        if (!throttler.Tick()) return;
        var side = _isClient ? "Client" : "Server";
        var send = _isSend ? "send" : "receive";
        _logger?.Log(level, $"{side} {send} packet: {typeof(T).Name}");
    }

    public void LogError<T>(T packet) where T : Packet => Log(LogLevel.Error, packet);

    public void LogWarning<T>(T packet) where T : Packet => Log(LogLevel.Warning, packet);

    public void LogDebug<T>(T packet) where T : Packet => Log(LogLevel.Debug, packet);

    public void LogInfo<T>(T packet) where T : Packet => Log(LogLevel.Info, packet);
}
