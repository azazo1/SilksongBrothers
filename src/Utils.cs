using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BepInEx.Logging;

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
    public static long Time => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _timeOffset;

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
        if (curTime - _lastActivatedTime > interval)
        {
            return true;
        }

        _lastActivatedTime = curTime;

        return false;
    }
}
