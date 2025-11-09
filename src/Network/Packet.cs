using MemoryPack;

namespace SilksongBrothers.Network;


[MemoryPackable]
public partial struct Packet
{
    /// <summary>
    /// peer id, 目标发送到的 peer.
    /// </summary>
    string DstPeer;
    /// <summary>
    /// peer id, 来源 peer.
    /// </summary>
    string SrcPeer;
    /// <summary>
    /// Packet 时的创建时间戳.
    /// </summary>
    long Time;
    /// <summary>
    /// 是否为实时包.
    /// </summary>
    /// 如果为 true, 那么此包在过期后接收到将被丢弃.
    bool RealTime;
    /// <summary>
    /// 实际发送的数据.
    /// </summary>
    byte[] Data;
}
