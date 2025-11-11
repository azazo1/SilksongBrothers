using System;
using MemoryPack;
using SilksongBrothers.Network;

namespace SilksongBrothers;

[MemoryPackable]
[MemoryPackUnion(0, typeof(PeerQuitPacket))]
[MemoryPackUnion(1, typeof(PeerIdPacket))]
[MemoryPackUnion(2, typeof(SyncTimePacket))]
[MemoryPackUnion(3, typeof(HeartbeatPacket))]
public abstract partial class Packet
{
    /// <summary>
    /// Packet 时的创建时间戳.
    /// </summary>
    [MemoryPackOrder(0)] public long Time = Utils.ServerTime;

    /// <summary>
    /// 是否为实时包.
    /// </summary>
    /// 如果为 true, 那么此包在过期后接收到将被丢弃.
    [MemoryPackOrder(1)] public bool IsRealtime = false;

    /// <summary>
    /// peer id, 目标发送到的 peers.
    /// </summary>
    /// 当为广播时, 为 null.
    /// 如果需要给服务端发送消息, 提供服务端的 peer id 即可.
    [MemoryPackOrder(2)] public string[]? DstPeer;

    /// <summary>
    /// peer id, 来源 peer.
    /// </summary>
    /// 服务器下发 packet 时会重新写入 SrcPeer 为其来源连接对应的 peer id.
    ///
    /// 客户端发送时除了 PeerIdPacket 之外可以不填写.
    [MemoryPackOrder(3)] public string? SrcPeer;
}

[MemoryPackable]
public partial class PeerQuitPacket : Packet;

/// <summary>
/// 向其他 peer 和服务端发送自身的 peer id.
///
/// 此消息可用广播的方式发送.
///
/// 服务端会根据特定连接第一次发送的此类型消息来标记一个连接的 PeerId.
/// </summary>
[MemoryPackable]
public partial class PeerIdPacket : Packet
{
    /// <summary>
    /// 玩家名.
    /// </summary>
    public string Name;

    /// <summary>
    /// 版本号, 用于检查当前用户和其他用户是否符合版本号要求.
    /// 只要具有相同的 major 和 minor 版本号, 那么 peers 之间就能兼容.
    /// </summary>
    public Version Version = Utils.Version;

    /// <summary>
    /// 标记此包是否需要回复, 防止无尽的递归回复.
    /// </summary>
    public bool NeedResponse;

    public PeerIdPacket(bool needResponse)
    {
        if (ModConfig.NetworkMode == NetworkMode.Standalone)
        {
            SrcPeer = ModConfig.StandalonePeerId;
        }

        Name = ModConfig.PlayerName;
        NeedResponse = needResponse;
    }
}

/// <summary>
/// 时间同步包, 客户端向服务端发送自身的 unix 时间, 之后服务器向客户端发送同类型的包以返回服务端的 unix 时间,
/// 都储存在 <see cref="Packet.Time"/> 之中.
/// <see cref="Packet.DstPeer"/> 和 <see cref="Packet.SrcPeer"/> 将会被忽视.
///
/// 此包不推荐进行预发送, 即不推荐连接建立之前就将此包放入发送缓冲区, 可能导致较大的误差.
/// </summary>
[MemoryPackable]
public partial class SyncTimePacket : Packet
{
    public SyncTimePacket()
    {
        Time = Utils.Time;
    }
}

/// <summary>
/// 心跳包, 客户端向服务端发送, 其 DstPeer 会被忽略, 服务端不会将心跳包发送给其他 Peer.
/// </summary>
[MemoryPackable]
public partial class HeartbeatPacket : Packet;
